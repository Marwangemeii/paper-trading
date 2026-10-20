using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using PaperTrading.Api.Engine;
using PaperTrading.Api.Realtime;

namespace PaperTrading.Api.Feed;

/// <summary>
/// Reads prices from the configured source and broadcasts each one to the rest of the system:
/// the matching engine (so resting limit orders can fill) and every connected browser.
/// In Auto mode it uses Binance and falls back to the mock feed after repeated failures
/// (e.g. Binance blocks the hosting region), continuing from the last real prices.
/// </summary>
public sealed partial class PriceFeedService(
    IOptions<PriceFeedOptions> options,
    BinancePriceFeed binanceFeed,
    MockPriceFeed mockFeed,
    EngineHost engine,
    IHubContext<MarketHub> hub,
    TimeProvider time,
    ILogger<PriceFeedService> logger) : BackgroundService
{
    private const int AutoFallbackAfterFailures = 3;

    private readonly Dictionary<string, decimal> _lastPrices = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mode = options.Value.Mode;
        if (mode == PriceFeedMode.None)
        {
            LogDisabled();
            return;
        }

        if (mode is PriceFeedMode.Binance or PriceFeedMode.Auto)
        {
            await RunBinanceAsync(keepRetrying: mode == PriceFeedMode.Binance, stoppingToken);
        }

        if (!stoppingToken.IsCancellationRequested)
        {
            LogUsingMock();
            await foreach (var tick in mockFeed.StreamAsync(_lastPrices, stoppingToken))
            {
                await PublishAsync(tick, stoppingToken);
            }
        }
    }

    private async Task RunBinanceAsync(bool keepRetrying, CancellationToken ct)
    {
        var failures = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                LogConnecting(options.Value.BinanceUrl);
                await foreach (var tick in binanceFeed.StreamAsync(ct))
                {
                    failures = 0;
                    await PublishAsync(tick, ct);
                }

                failures++;
                LogBinanceClosed(failures);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                failures++;
                LogBinanceFailed(ex, failures);
            }

            if (!keepRetrying && failures >= AutoFallbackAfterFailures)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, 2 * failures)), time, ct);
        }
    }

    private async Task PublishAsync(PriceTick tick, CancellationToken ct)
    {
        if (!engine.IsKnownSymbol(tick.Symbol))
        {
            return;
        }

        _lastPrices[tick.Symbol] = tick.Price;
        var filled = await engine.ApplyPriceTickAsync(tick, ct);

        await hub.Clients.All.SendAsync(MarketHub.PriceEvent, tick, ct);
        foreach (var order in filled)
        {
            await hub.Clients.Group(MarketHub.AccountGroup(order.AccountId))
                .SendAsync(MarketHub.OrderUpdatedEvent, order, ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Price feed disabled")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to Binance at {Url}")]
    private partial void LogConnecting(Uri url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Binance stream closed (failure {Failures})")]
    private partial void LogBinanceClosed(int failures);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Binance stream failed (failure {Failures})")]
    private partial void LogBinanceFailed(Exception ex, int failures);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Using mock price feed")]
    private partial void LogUsingMock();
}
