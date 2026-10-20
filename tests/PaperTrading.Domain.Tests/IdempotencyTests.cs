using PaperTrading.Domain;

namespace PaperTrading.Domain.Tests;

public sealed class IdempotencyTests : EngineTestBase
{
    [Fact]
    public void Retrying_with_the_same_key_returns_the_original_order_and_charges_once()
    {
        Tick(100m);

        var first = Place(OrderSide.Buy, OrderType.Market, 1m, key: "retry-me");
        var retry = Place(OrderSide.Buy, OrderType.Market, 1m, key: "retry-me");

        Assert.Equal(PlaceOrderOutcome.Created, first.Outcome);
        Assert.Equal(PlaceOrderOutcome.Replayed, retry.Outcome);
        Assert.Same(first.Order, retry.Order);
        Assert.Equal(StartingCash - 100m, Account.Cash);
    }

    [Fact]
    public void Reusing_a_key_with_a_different_payload_is_a_conflict_and_changes_nothing()
    {
        Tick(100m);
        Place(OrderSide.Buy, OrderType.Market, 1m, key: "k");

        var conflict = Place(OrderSide.Buy, OrderType.Market, 2m, key: "k");

        Assert.Equal(PlaceOrderOutcome.IdempotencyConflict, conflict.Outcome);
        Assert.Equal(1m, Account.FindPosition(Eth)!.Quantity);
    }

    [Fact]
    public void Symbol_case_does_not_break_a_replay()
    {
        Tick(100m);
        Place(OrderSide.Buy, OrderType.Market, 1m, symbol: "ETHUSDT", key: "k");

        var retry = Place(OrderSide.Buy, OrderType.Market, 1m, symbol: "ethusdt", key: "k");

        Assert.Equal(PlaceOrderOutcome.Replayed, retry.Outcome);
    }

    [Fact]
    public void Keys_are_scoped_per_account()
    {
        Tick(100m);
        var otherAccount = Guid.NewGuid();
        Engine.OpenAccount(otherAccount, 1_000m);

        var mine = Place(OrderSide.Buy, OrderType.Market, 1m, key: "shared");
        var theirs = Place(OrderSide.Buy, OrderType.Market, 1m, key: "shared", accountId: otherAccount);

        Assert.Equal(PlaceOrderOutcome.Created, theirs.Outcome);
        Assert.NotEqual(mine.Order!.Id, theirs.Order!.Id);
        Assert.Equal(900m, Engine.GetAccount(otherAccount)!.Cash);
    }

    [Fact]
    public void An_invalid_request_does_not_consume_the_key()
    {
        Tick(100m);

        var invalid = Place(OrderSide.Buy, OrderType.Market, 0m, key: "k");
        var valid = Place(OrderSide.Buy, OrderType.Market, 1m, key: "k");

        Assert.Equal(PlaceOrderOutcome.Invalid, invalid.Outcome);
        Assert.Equal(PlaceOrderOutcome.Created, valid.Outcome);
    }

    [Fact]
    public void A_rejected_order_replays_as_rejected_even_if_it_would_now_succeed()
    {
        // Matches common payment-API semantics: a key stands for one decision, forever.
        var rejected = Place(OrderSide.Buy, OrderType.Market, 1m, key: "k"); // no price yet
        Tick(100m);

        var retry = Place(OrderSide.Buy, OrderType.Market, 1m, key: "k");

        Assert.Equal(OrderStatus.Rejected, rejected.Order!.Status);
        Assert.Equal(PlaceOrderOutcome.Replayed, retry.Outcome);
        Assert.Equal(OrderStatus.Rejected, retry.Order!.Status);
        Assert.Equal(StartingCash, Account.Cash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_key_is_invalid(string key)
    {
        var result = Place(OrderSide.Buy, OrderType.Market, 1m, key: key);

        Assert.Equal(PlaceOrderOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void An_overlong_key_is_invalid()
    {
        var key = new string('x', MatchingEngine.MaxIdempotencyKeyLength + 1);

        var result = Place(OrderSide.Buy, OrderType.Market, 1m, key: key);

        Assert.Equal(PlaceOrderOutcome.Invalid, result.Outcome);
    }
}
