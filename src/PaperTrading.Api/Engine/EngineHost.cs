using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaperTrading.Api.Persistence;
using PaperTrading.Domain;

namespace PaperTrading.Api.Engine;

/// <summary>
/// The only way into the matching engine. Every command (orders, cancels, price ticks, reads)
/// waits in one first-in-first-out queue, and a single background worker runs them one at a
/// time. Each change is saved to the database before the caller gets its answer.
/// <para>
/// This is what makes concurrent requests safe: the engine itself has no locks and is only ever
/// touched by the worker. It also gives a fair, ordered history of events (arrival order).
/// </para>
/// </summary>
public sealed partial class EngineHost : BackgroundService
{
    private readonly Channel<WorkItem> _queue =
        Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions { SingleReader = true });

    private readonly MatchingEngine _engine;
    private readonly IDbContextFactory<TradingDbContext> _dbFactory;
    private readonly ILogger<EngineHost> _logger;
    private readonly decimal _startingCash;
    private TradingDbContext? _db;

    public EngineHost(
        IOptions<TradingOptions> options,
        IDbContextFactory<TradingDbContext> dbFactory,
        TimeProvider time,
        ILogger<EngineHost> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _engine = new MatchingEngine(options.Value.Instruments.Select(i => i.ToInstrument()), time);
        _startingCash = options.Value.StartingCash;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public bool IsKnownSymbol(string symbol) => _engine.Instruments.Any(i => i.Symbol == symbol);

    public Task<AccountCreatedResponse> OpenAccountAsync(CancellationToken ct) =>
        EnqueueAsync(writes: true, ct: ct, work: () =>
        {
            var account = _engine.OpenAccount(Guid.NewGuid(), _startingCash);
            Db.Accounts.Add(account);
            return new AccountCreatedResponse(account.Id, account.Cash);
        });

    public Task<PlaceOrderReply> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken ct) =>
        EnqueueAsync(writes: true, ct: ct, work: () =>
        {
            var result = _engine.PlaceOrder(command);
            if (result.Outcome == PlaceOrderOutcome.Created)
            {
                Db.Orders.Add(result.Order!);
            }

            return new PlaceOrderReply(
                result.Outcome,
                result.Order is null ? null : OrderDto.From(result.Order),
                result.Error);
        });

    public Task<CancelOrderReply> CancelOrderAsync(Guid accountId, Guid orderId, CancellationToken ct) =>
        EnqueueAsync(writes: true, ct: ct, work: () =>
        {
            var result = _engine.CancelOrder(accountId, orderId);
            return new CancelOrderReply(result.Outcome, result.Order is null ? null : OrderDto.From(result.Order));
        });

    /// <returns>The orders this price filled.</returns>
    public Task<IReadOnlyList<OrderDto>> ApplyPriceTickAsync(PriceTick tick, CancellationToken ct)
    {
        // Checked here, on the caller's side, so bad feed data can never stop the worker.
        ArgumentNullException.ThrowIfNull(tick);
        if (!IsKnownSymbol(tick.Symbol) || tick.Price <= 0)
        {
            throw new ArgumentException($"Invalid tick {tick.Symbol} @ {tick.Price}.", nameof(tick));
        }

        return EnqueueAsync<IReadOnlyList<OrderDto>>(writes: true, ct: ct, work: () =>
            [.. _engine.ApplyPriceTick(tick.Symbol, tick.Price).Select(OrderDto.From)]);
    }

    public Task<PortfolioSnapshot?> GetPortfolioAsync(Guid accountId, CancellationToken ct) =>
        EnqueueAsync(writes: false, ct: ct, work: () => _engine.GetPortfolio(accountId));

    public Task<IReadOnlyList<InstrumentDto>> GetInstrumentsAsync(CancellationToken ct) =>
        EnqueueAsync<IReadOnlyList<InstrumentDto>>(writes: false, ct: ct, work: () =>
        [
            .. _engine.Instruments.Select(i => new InstrumentDto(
                i.Symbol, i.BaseAsset, Money.QuoteCurrency, i.QuantityStep, i.TickSize, _engine.GetLastPrice(i.Symbol))),
        ]);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);
        await db.Database.EnsureCreatedAsync(stoppingToken);
        // WAL lets the order-history endpoint read while the worker writes.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", stoppingToken);

        var accounts = await db.Accounts.Include(a => a.Positions).ToListAsync(stoppingToken);
        var orders = await db.Orders.ToListAsync(stoppingToken);
        _engine.Restore(accounts, orders);
        _db = db;
        LogRestored(accounts.Count, orders.Count);

        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                item.Execute();
                if (item.Writes)
                {
                    await db.SaveChangesAsync(stoppingToken);
                }

                item.Complete();
            }
            catch (Exception ex)
            {
                // Fail fast: after an unexpected error, memory and database may disagree.
                // Stopping is safer than trading on a possibly wrong balance; on restart the
                // engine is rebuilt from the last saved state, and clients retry with the
                // same idempotency key.
                LogFatal(ex);
                item.Fail(ex);
                _queue.Writer.TryComplete(ex);
                while (_queue.Reader.TryRead(out var pending))
                {
                    pending.Fail(ex);
                }

                throw;
            }
        }
    }

    private TradingDbContext Db =>
        _db ?? throw new InvalidOperationException("The engine worker has not started yet.");

    private Task<T> EnqueueAsync<T>(bool writes, Func<T> work, CancellationToken ct)
    {
        var item = new WorkItem<T>(work, writes);
        if (!_queue.Writer.TryWrite(item))
        {
            throw new InvalidOperationException("The engine is not accepting commands.");
        }

        // If the caller gives up (e.g. the browser disconnects) the command still runs;
        // a retry with the same idempotency key will return its result.
        return item.Task.WaitAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine restored {Accounts} accounts and {Orders} orders")]
    private partial void LogRestored(int accounts, int orders);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Engine worker failed; stopping to protect account state")]
    private partial void LogFatal(Exception ex);

    private abstract class WorkItem(bool writes)
    {
        public bool Writes { get; } = writes;

        public abstract void Execute();

        public abstract void Complete();

        public abstract void Fail(Exception ex);
    }

    private sealed class WorkItem<T>(Func<T> work, bool writes) : WorkItem(writes)
    {
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private T? _result;

        public Task<T> Task => _completion.Task;

        public override void Execute() => _result = work();

        public override void Complete() => _completion.TrySetResult(_result!);

        public override void Fail(Exception ex) => _completion.TrySetException(ex);
    }
}
