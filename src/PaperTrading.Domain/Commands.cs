namespace PaperTrading.Domain;

public sealed record PlaceOrderCommand(
    Guid AccountId,
    string IdempotencyKey,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? LimitPrice);

public enum PlaceOrderOutcome
{
    /// <summary>A new order was recorded. Its status may be Filled, Open or Rejected.</summary>
    Created,
    /// <summary>The idempotency key was already used with the same payload; the original order is returned.</summary>
    Replayed,
    /// <summary>The idempotency key was already used with a different payload.</summary>
    IdempotencyConflict,
    /// <summary>The request is malformed. Nothing was recorded and the key is not consumed.</summary>
    Invalid,
    AccountNotFound,
}

public sealed record PlaceOrderResult(PlaceOrderOutcome Outcome, Order? Order = null, string? Error = null);

public enum CancelOrderOutcome
{
    Cancelled,
    NotFound,
    NotOpen,
}

public sealed record CancelOrderResult(CancelOrderOutcome Outcome, Order? Order = null);
