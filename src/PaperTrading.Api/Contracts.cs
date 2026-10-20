using PaperTrading.Domain;

namespace PaperTrading.Api;

// Shapes sent over HTTP/SignalR. Domain objects never leave the engine worker, so a response can
// never observe an order or account while the worker is changing it.

public sealed record PlaceOrderRequest(
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? LimitPrice);

public sealed record AccountCreatedResponse(Guid AccountId, decimal StartingCash);

public sealed record OrderDto(
    Guid Id,
    Guid AccountId,
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? LimitPrice,
    OrderStatus Status,
    decimal? FillPrice,
    decimal? Notional,
    string? RejectReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt)
{
    public static OrderDto From(Order order) => new(
        order.Id,
        order.AccountId,
        order.Symbol,
        order.Side,
        order.Type,
        order.Quantity,
        order.LimitPrice,
        order.Status,
        order.FillPrice,
        order.Notional,
        order.RejectReason,
        order.CreatedAt,
        order.ClosedAt);
}

public sealed record InstrumentDto(
    string Symbol,
    string BaseAsset,
    string QuoteAsset,
    decimal QuantityStep,
    decimal TickSize,
    decimal? LastPrice);

public sealed record PriceTick(string Symbol, decimal Price, DateTimeOffset At);

public sealed record PlaceOrderReply(PlaceOrderOutcome Outcome, OrderDto? Order, string? Error);

public sealed record CancelOrderReply(CancelOrderOutcome Outcome, OrderDto? Order);
