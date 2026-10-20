namespace PaperTrading.Domain;

public sealed class Order
{
    // Required by EF Core for materialisation.
    private Order() { }

    internal Order(long sequence, PlaceOrderCommand command, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Sequence = sequence;
        AccountId = command.AccountId;
        IdempotencyKey = command.IdempotencyKey;
        Symbol = command.Symbol;
        Side = command.Side;
        Type = command.Type;
        Quantity = command.Quantity;
        LimitPrice = command.LimitPrice;
        Status = OrderStatus.Open;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    /// <summary>Monotonic arrival number; gives FIFO priority among equal limit prices.</summary>
    public long Sequence { get; private set; }

    public Guid AccountId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = string.Empty;
    public OrderSide Side { get; private set; }
    public OrderType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal? LimitPrice { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal? FillPrice { get; private set; }
    public string? RejectReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Cash held aside while a limit buy is resting: quantity × limit price.</summary>
    public decimal ReservedCash =>
        Status == OrderStatus.Open && Side == OrderSide.Buy && LimitPrice is { } limit ? Quantity * limit : 0m;

    /// <summary>Base-asset quantity held aside while a limit sell is resting.</summary>
    public decimal ReservedQuantity =>
        Status == OrderStatus.Open && Side == OrderSide.Sell ? Quantity : 0m;

    /// <summary>Executed value (quantity × fill price), or null if not filled.</summary>
    public decimal? Notional => FillPrice * Quantity;

    /// <summary>True when the price has reached this limit order's trigger level.</summary>
    internal bool IsTriggeredBy(decimal price) => Side == OrderSide.Buy
        ? price <= LimitPrice
        : price >= LimitPrice;

    internal bool HasSamePayload(PlaceOrderCommand command) =>
        Symbol == command.Symbol
        && Side == command.Side
        && Type == command.Type
        && Quantity == command.Quantity
        && LimitPrice == command.LimitPrice;

    internal void MarkFilled(decimal price, DateTimeOffset at)
    {
        EnsureOpen();
        Status = OrderStatus.Filled;
        FillPrice = price;
        ClosedAt = at;
    }

    internal void MarkRejected(string reason, DateTimeOffset at)
    {
        EnsureOpen();
        Status = OrderStatus.Rejected;
        RejectReason = reason;
        ClosedAt = at;
    }

    internal void MarkCancelled(DateTimeOffset at)
    {
        EnsureOpen();
        Status = OrderStatus.Cancelled;
        ClosedAt = at;
    }

    private void EnsureOpen()
    {
        if (Status != OrderStatus.Open)
        {
            throw new InvalidOperationException($"Order {Id} is {Status} and can no longer change.");
        }
    }
}
