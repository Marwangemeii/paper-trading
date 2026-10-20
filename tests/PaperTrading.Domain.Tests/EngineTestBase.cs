using PaperTrading.Domain;

namespace PaperTrading.Domain.Tests;

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Shared setup: one engine with BTC and ETH, and one account holding 100,000 USDT.</summary>
public abstract class EngineTestBase
{
    protected const string Btc = "BTCUSDT";
    protected const string Eth = "ETHUSDT";
    protected const decimal StartingCash = 100_000m;

    internal static readonly Instrument[] Instruments =
    [
        new(Btc, "BTC", QuantityStep: 0.00001m, TickSize: 0.01m),
        new(Eth, "ETH", QuantityStep: 0.0001m, TickSize: 0.01m),
    ];

    private int _keyCounter;

    protected EngineTestBase()
    {
        Engine = new MatchingEngine(Instruments, Clock);
        Engine.OpenAccount(AccountId, StartingCash);
    }

    internal TestClock Clock { get; } = new();
    protected MatchingEngine Engine { get; }
    protected Guid AccountId { get; } = Guid.NewGuid();
    protected Account Account => Engine.GetAccount(AccountId)!;

    protected PlaceOrderResult Place(
        OrderSide side,
        OrderType type,
        decimal quantity,
        decimal? limitPrice = null,
        string symbol = Eth,
        string? key = null,
        Guid? accountId = null) =>
        Engine.PlaceOrder(new PlaceOrderCommand(
            accountId ?? AccountId,
            key ?? $"key-{++_keyCounter}",
            symbol,
            side,
            type,
            quantity,
            limitPrice));

    /// <summary>Places an order that is expected to be recorded, and returns it.</summary>
    protected Order Market(OrderSide side, decimal quantity, string symbol = Eth)
    {
        var result = Place(side, OrderType.Market, quantity, symbol: symbol);
        Assert.Equal(PlaceOrderOutcome.Created, result.Outcome);
        return result.Order!;
    }

    protected Order Limit(OrderSide side, decimal quantity, decimal limitPrice, string symbol = Eth)
    {
        var result = Place(side, OrderType.Limit, quantity, limitPrice, symbol);
        Assert.Equal(PlaceOrderOutcome.Created, result.Outcome);
        return result.Order!;
    }

    protected IReadOnlyList<Order> Tick(decimal price, string symbol = Eth) => Engine.ApplyPriceTick(symbol, price);
}
