namespace PaperTrading.Domain;

/// <summary>
/// Simulates execution of orders against a live price stream.
/// <para>
/// Not thread-safe by design: the host must feed it one command at a time (single writer).
/// Serialising all state changes through one place is what makes concurrent submissions
/// correct without fine-grained locks, and it keeps this class deterministic and easy to test.
/// </para>
/// Execution model: market orders fill in full at the last price; limit orders reserve funds
/// (cash or quantity) when accepted and fill in full at the first price that reaches the limit.
/// </summary>
public sealed class MatchingEngine
{
    public const int MaxIdempotencyKeyLength = 64;

    private readonly TimeProvider _time;
    private readonly Dictionary<string, Instrument> _instruments;
    private readonly Dictionary<string, OrderBook> _books;
    private readonly Dictionary<string, decimal> _lastPrices = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Account> _accounts = [];
    private readonly Dictionary<Guid, Order> _orders = [];
    private readonly Dictionary<(Guid AccountId, string Key), Order> _ordersByIdempotencyKey = [];
    private long _sequence;

    public MatchingEngine(IEnumerable<Instrument> instruments, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(instruments);
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _instruments = instruments.ToDictionary(i => i.Symbol, StringComparer.Ordinal);
        _books = _instruments.Keys.ToDictionary(s => s, _ => new OrderBook(), StringComparer.Ordinal);
    }

    public IReadOnlyCollection<Instrument> Instruments => _instruments.Values;

    /// <summary>
    /// Rebuilds in-memory state from storage on startup: accounts, every order (so idempotency
    /// keys survive a restart) and the order books from the orders that are still open.
    /// </summary>
    public void Restore(IEnumerable<Account> accounts, IEnumerable<Order> orders)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(orders);
        if (_accounts.Count > 0 || _orders.Count > 0)
        {
            throw new InvalidOperationException("Restore must run before any other command.");
        }

        foreach (var account in accounts)
        {
            _accounts.Add(account.Id, account);
        }

        foreach (var order in orders.OrderBy(o => o.Sequence))
        {
            _orders.Add(order.Id, order);
            _ordersByIdempotencyKey.Add((order.AccountId, order.IdempotencyKey), order);
            _sequence = Math.Max(_sequence, order.Sequence);

            if (order.Status == OrderStatus.Open)
            {
                var book = _books.GetValueOrDefault(order.Symbol)
                    ?? throw new InvalidOperationException($"Open order {order.Id} uses unconfigured symbol {order.Symbol}.");
                book.Add(order);
            }
        }
    }

    public Account OpenAccount(Guid accountId, decimal initialCash)
    {
        if (_accounts.ContainsKey(accountId))
        {
            throw new InvalidOperationException($"Account {accountId} already exists.");
        }

        var account = Account.Open(accountId, initialCash, _time.GetUtcNow());
        _accounts.Add(accountId, account);
        return account;
    }

    public Account? GetAccount(Guid accountId) => _accounts.GetValueOrDefault(accountId);

    public Order? GetOrder(Guid orderId) => _orders.GetValueOrDefault(orderId);

    public decimal? GetLastPrice(string symbol) =>
        _lastPrices.TryGetValue(symbol, out var price) ? price : null;

    public PortfolioSnapshot? GetPortfolio(Guid accountId) => GetAccount(accountId)?.Valuate(GetLastPrice);

    public PlaceOrderResult PlaceOrder(PlaceOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command = command with { Symbol = (command.Symbol ?? string.Empty).Trim().ToUpperInvariant() };

        if (!_accounts.TryGetValue(command.AccountId, out var account))
        {
            return new PlaceOrderResult(PlaceOrderOutcome.AccountNotFound);
        }

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > MaxIdempotencyKeyLength)
        {
            return Invalid($"Idempotency key is required and must be at most {MaxIdempotencyKeyLength} characters.");
        }

        // Idempotency: the same key from the same account always maps to the same order.
        if (_ordersByIdempotencyKey.TryGetValue((command.AccountId, command.IdempotencyKey), out var existing))
        {
            return existing.HasSamePayload(command)
                ? new PlaceOrderResult(PlaceOrderOutcome.Replayed, existing)
                : new PlaceOrderResult(
                    PlaceOrderOutcome.IdempotencyConflict,
                    existing,
                    "Idempotency key was already used with a different order.");
        }

        if (Validate(command) is { } error)
        {
            return Invalid(error);
        }

        var order = new Order(++_sequence, command, _time.GetUtcNow());
        _orders.Add(order.Id, order);
        _ordersByIdempotencyKey.Add((order.AccountId, order.IdempotencyKey), order);

        if (order.Type == OrderType.Market)
        {
            ExecuteMarket(order, account);
        }
        else
        {
            AcceptLimit(order, account);
        }

        return new PlaceOrderResult(PlaceOrderOutcome.Created, order);
    }

    public CancelOrderResult CancelOrder(Guid accountId, Guid orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order) || order.AccountId != accountId)
        {
            return new CancelOrderResult(CancelOrderOutcome.NotFound);
        }

        if (order.Status != OrderStatus.Open)
        {
            return new CancelOrderResult(CancelOrderOutcome.NotOpen, order);
        }

        _books[order.Symbol].Remove(order);
        ReleaseReservation(order, _accounts[accountId]);
        order.MarkCancelled(_time.GetUtcNow());
        return new CancelOrderResult(CancelOrderOutcome.Cancelled, order);
    }

    /// <summary>Records a new market price and fills every resting order it triggers.</summary>
    /// <returns>The orders filled by this tick, in arrival order.</returns>
    public IReadOnlyList<Order> ApplyPriceTick(string symbol, decimal price)
    {
        if (!_books.TryGetValue(symbol, out var book))
        {
            throw new ArgumentException($"Unknown symbol '{symbol}'.", nameof(symbol));
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, "Price must be positive.");
        }

        _lastPrices[symbol] = price;

        var triggered = book.TakeTriggered(price);
        foreach (var order in triggered)
        {
            FillResting(order, _accounts[order.AccountId], price);
        }

        return triggered;
    }

    private string? Validate(PlaceOrderCommand command)
    {
        if (!_instruments.TryGetValue(command.Symbol, out var instrument))
        {
            return $"Unknown symbol '{command.Symbol}'.";
        }

        if (!instrument.IsValidQuantity(command.Quantity))
        {
            return $"Quantity must be positive and a multiple of {instrument.QuantityStep}.";
        }

        return command.Type switch
        {
            OrderType.Market when command.LimitPrice is not null => "Market orders must not specify a limit price.",
            OrderType.Limit when command.LimitPrice is null => "Limit orders require a limit price.",
            OrderType.Limit when !instrument.IsValidPrice(command.LimitPrice.Value) =>
                $"Limit price must be positive and a multiple of {instrument.TickSize}.",
            OrderType.Market or OrderType.Limit => null,
            _ => $"Unsupported order type '{command.Type}'.",
        };
    }

    private void ExecuteMarket(Order order, Account account)
    {
        if (GetLastPrice(order.Symbol) is not { } price)
        {
            order.MarkRejected(RejectReasons.NoMarketPrice, _time.GetUtcNow());
            return;
        }

        if (order.Side == OrderSide.Buy)
        {
            if (order.Quantity * price > account.AvailableCash)
            {
                order.MarkRejected(RejectReasons.InsufficientFunds, _time.GetUtcNow());
                return;
            }

            account.SettleBuy(order.Symbol, order.Quantity, price);
        }
        else
        {
            if (order.Quantity > AvailableQuantity(account, order.Symbol))
            {
                order.MarkRejected(RejectReasons.InsufficientPosition, _time.GetUtcNow());
                return;
            }

            account.SettleSell(order.Symbol, order.Quantity, price);
        }

        order.MarkFilled(price, _time.GetUtcNow());
    }

    private void AcceptLimit(Order order, Account account)
    {
        // Reserve first, so a resting order can never fail to settle later and the same
        // cash or coins cannot be promised to two orders.
        if (order.Side == OrderSide.Buy)
        {
            var amount = order.ReservedCash;
            if (amount > account.AvailableCash)
            {
                order.MarkRejected(RejectReasons.InsufficientFunds, _time.GetUtcNow());
                return;
            }

            account.ReserveCash(amount);
        }
        else
        {
            if (order.Quantity > AvailableQuantity(account, order.Symbol))
            {
                order.MarkRejected(RejectReasons.InsufficientPosition, _time.GetUtcNow());
                return;
            }

            account.ReserveQuantity(order.Symbol, order.Quantity);
        }

        // A limit that is already marketable fills straight away at the current (better or equal) price.
        if (GetLastPrice(order.Symbol) is { } price && order.IsTriggeredBy(price))
        {
            FillResting(order, account, price);
        }
        else
        {
            _books[order.Symbol].Add(order);
        }
    }

    /// <summary>
    /// Fills a limit order at <paramref name="price"/>. For a buy, price ≤ limit, so the
    /// actual cost never exceeds the reserved cash and the difference returns to available cash.
    /// </summary>
    private void FillResting(Order order, Account account, decimal price)
    {
        ReleaseReservation(order, account);

        if (order.Side == OrderSide.Buy)
        {
            account.SettleBuy(order.Symbol, order.Quantity, price);
        }
        else
        {
            account.SettleSell(order.Symbol, order.Quantity, price);
        }

        order.MarkFilled(price, _time.GetUtcNow());
    }

    private static void ReleaseReservation(Order order, Account account)
    {
        if (order.Side == OrderSide.Buy)
        {
            account.ReleaseCash(order.ReservedCash);
        }
        else
        {
            account.ReleaseQuantity(order.Symbol, order.ReservedQuantity);
        }
    }

    private static decimal AvailableQuantity(Account account, string symbol) =>
        account.FindPosition(symbol)?.AvailableQuantity ?? 0m;

    private static PlaceOrderResult Invalid(string error) => new(PlaceOrderOutcome.Invalid, Error: error);
}
