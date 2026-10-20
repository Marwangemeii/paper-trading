using PaperTrading.Domain;

namespace PaperTrading.Domain.Tests;

public sealed class RestoreTests : EngineTestBase
{
    [Fact]
    public void A_restored_engine_keeps_resting_orders_balances_and_idempotency_keys()
    {
        Tick(100m);
        var filled = Place(OrderSide.Buy, OrderType.Market, 1m, key: "market").Order!;
        var resting = Place(OrderSide.Buy, OrderType.Limit, 2m, 90m, key: "limit").Order!;

        // Simulates a restart: a brand-new engine loaded with what storage would return.
        var restarted = new MatchingEngine(Instruments, Clock);
        restarted.Restore([Account], [filled, resting]);

        var replay = restarted.PlaceOrder(new PlaceOrderCommand(
            AccountId, "market", Eth, OrderSide.Buy, OrderType.Market, 1m, null));
        var fills = restarted.ApplyPriceTick(Eth, 89m);
        var next = restarted.PlaceOrder(new PlaceOrderCommand(
            AccountId, "after-restart", Eth, OrderSide.Buy, OrderType.Market, 1m, null));

        Assert.Equal(PlaceOrderOutcome.Replayed, replay.Outcome);
        Assert.Equal([resting], fills);
        Assert.Equal(OrderStatus.Filled, resting.Status);
        Assert.Equal(0m, Account.ReservedCash);
        Assert.True(next.Order!.Sequence > resting.Sequence, "sequence numbers continue after restore");
    }

    [Fact]
    public void Restore_is_only_allowed_on_an_empty_engine()
    {
        Assert.Throws<InvalidOperationException>(() => Engine.Restore([], []));
    }

    [Fact]
    public void A_missing_symbol_is_invalid_rather_than_a_crash()
    {
        var result = Place(OrderSide.Buy, OrderType.Market, 1m, symbol: null!);

        Assert.Equal(PlaceOrderOutcome.Invalid, result.Outcome);
    }
}
