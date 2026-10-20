using PaperTrading.Domain;

namespace PaperTrading.Domain.Tests;

public sealed class LimitOrderTests : EngineTestBase
{
    [Fact]
    public void Limit_buy_below_market_rests_and_reserves_cash()
    {
        Tick(100m);

        var order = Limit(OrderSide.Buy, 2m, 90m);

        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(StartingCash, Account.Cash);
        Assert.Equal(180m, Account.ReservedCash);
        Assert.Equal(StartingCash - 180m, Account.AvailableCash);
    }

    [Fact]
    public void Limit_buy_waits_while_price_stays_above_limit()
    {
        Tick(100m);
        var order = Limit(OrderSide.Buy, 2m, 90m);

        var filled = Tick(90.01m);

        Assert.Empty(filled);
        Assert.Equal(OrderStatus.Open, order.Status);
    }

    [Fact]
    public void Limit_buy_fills_when_price_reaches_limit()
    {
        Tick(100m);
        var order = Limit(OrderSide.Buy, 2m, 90m);

        var filled = Tick(90m);

        Assert.Equal([order], filled);
        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(90m, order.FillPrice);
        Assert.Equal(0m, Account.ReservedCash);
        Assert.Equal(StartingCash - 180m, Account.Cash);
        Assert.Equal(2m, Account.FindPosition(Eth)!.Quantity);
    }

    [Fact]
    public void Limit_buy_fills_at_the_better_price_after_a_gap_and_returns_the_unused_reservation()
    {
        Tick(100m);
        var order = Limit(OrderSide.Buy, 2m, 90m); // reserves 180

        Tick(85m);

        Assert.Equal(85m, order.FillPrice);
        Assert.Equal(0m, Account.ReservedCash);
        Assert.Equal(StartingCash - 170m, Account.Cash); // paid 170, the other 10 is free again
    }

    [Fact]
    public void Marketable_limit_buy_fills_immediately_at_current_price()
    {
        Tick(100m);

        var order = Limit(OrderSide.Buy, 1m, 110m);

        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(100m, order.FillPrice);
        Assert.Equal(StartingCash - 100m, Account.Cash);
        Assert.Equal(0m, Account.ReservedCash);
    }

    [Fact]
    public void Limit_order_placed_before_any_price_rests_until_the_first_tick()
    {
        var order = Limit(OrderSide.Buy, 1m, 100m);
        Assert.Equal(OrderStatus.Open, order.Status);

        Tick(99m);

        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(99m, order.FillPrice);
    }

    [Fact]
    public void Limit_buy_is_rejected_when_reservation_exceeds_available_cash()
    {
        Tick(3_000m);

        var order = Limit(OrderSide.Buy, 50m, 2_000.01m); // needs 100,000.50

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal(RejectReasons.InsufficientFunds, order.RejectReason);
        Assert.Equal(0m, Account.ReservedCash);
    }

    [Fact]
    public void Reserved_cash_cannot_be_promised_twice()
    {
        Tick(70_000m, Btc);
        Limit(OrderSide.Buy, 1m, 60_000m, Btc); // 40,000 left available

        var secondLimit = Limit(OrderSide.Buy, 1m, 60_000m, Btc);
        var market = Market(OrderSide.Buy, 0.6m, Btc); // 42,000

        Assert.Equal(RejectReasons.InsufficientFunds, secondLimit.RejectReason);
        Assert.Equal(RejectReasons.InsufficientFunds, market.RejectReason);
        Assert.Equal(40_000m, Account.AvailableCash);
    }

    [Fact]
    public void Limit_sell_reserves_quantity_and_fills_when_price_rises_to_limit()
    {
        Tick(100m);
        Market(OrderSide.Buy, 1m);
        var sell = Limit(OrderSide.Sell, 1m, 120m);

        Assert.Equal(1m, Account.FindPosition(Eth)!.ReservedQuantity);
        Assert.Empty(Tick(119.99m));

        Tick(125m);

        Assert.Equal(OrderStatus.Filled, sell.Status);
        Assert.Equal(125m, sell.FillPrice);
        Assert.Equal(25m, Account.RealizedPnl);
        Assert.Equal(0m, Account.FindPosition(Eth)!.Quantity);
        Assert.Equal(0m, Account.FindPosition(Eth)!.ReservedQuantity);
    }

    [Fact]
    public void Limit_sell_is_rejected_when_quantity_is_not_available()
    {
        Tick(100m);
        Market(OrderSide.Buy, 1m);
        Limit(OrderSide.Sell, 0.6m, 150m);

        var second = Limit(OrderSide.Sell, 0.5m, 150m);

        Assert.Equal(RejectReasons.InsufficientPosition, second.RejectReason);
    }

    [Fact]
    public void Cancelling_a_limit_buy_releases_its_reserved_cash_and_it_never_fills()
    {
        Tick(100m);
        var order = Limit(OrderSide.Buy, 2m, 90m);

        var result = Engine.CancelOrder(AccountId, order.Id);
        var filled = Tick(80m);

        Assert.Equal(CancelOrderOutcome.Cancelled, result.Outcome);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(0m, Account.ReservedCash);
        Assert.Empty(filled);
        Assert.Equal(StartingCash, Account.Cash);
    }

    [Fact]
    public void Cancelling_a_limit_sell_releases_its_reserved_quantity()
    {
        Tick(100m);
        Market(OrderSide.Buy, 1m);
        var order = Limit(OrderSide.Sell, 1m, 150m);

        Engine.CancelOrder(AccountId, order.Id);

        Assert.Equal(0m, Account.FindPosition(Eth)!.ReservedQuantity);
        Assert.Equal(1m, Account.FindPosition(Eth)!.AvailableQuantity);
    }

    [Fact]
    public void Cancelling_a_filled_order_is_refused()
    {
        Tick(100m);
        var order = Market(OrderSide.Buy, 1m);

        var result = Engine.CancelOrder(AccountId, order.Id);

        Assert.Equal(CancelOrderOutcome.NotOpen, result.Outcome);
        Assert.Equal(OrderStatus.Filled, order.Status);
    }

    [Fact]
    public void Cancelling_another_accounts_order_is_reported_as_not_found()
    {
        Tick(100m);
        var order = Limit(OrderSide.Buy, 1m, 90m);

        var result = Engine.CancelOrder(Guid.NewGuid(), order.Id);

        Assert.Equal(CancelOrderOutcome.NotFound, result.Outcome);
        Assert.Equal(OrderStatus.Open, order.Status);
    }

    [Fact]
    public void A_tick_fills_only_triggered_orders_and_reports_them_in_arrival_order()
    {
        Tick(100m);
        var first = Limit(OrderSide.Buy, 1m, 95m);
        var second = Limit(OrderSide.Buy, 1m, 99m);
        var third = Limit(OrderSide.Buy, 1m, 95m);
        var notReached = Limit(OrderSide.Buy, 1m, 90m);

        Assert.Equal([second], Tick(97m));
        Assert.Equal([first, third], Tick(94m));

        Assert.Equal(OrderStatus.Open, notReached.Status);
        Assert.All([first, second, third], o => Assert.Equal(OrderStatus.Filled, o.Status));
        Assert.Equal(90m, Account.ReservedCash);
    }

    [Fact]
    public void Ticks_on_one_symbol_do_not_trigger_orders_on_another()
    {
        Tick(100m, Eth);
        Tick(70_000m, Btc);
        var ethOrder = Limit(OrderSide.Buy, 1m, 90m, Eth);

        Tick(50m, Btc);

        Assert.Equal(OrderStatus.Open, ethOrder.Status);
    }
}
