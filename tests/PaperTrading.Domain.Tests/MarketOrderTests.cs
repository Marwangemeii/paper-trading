using PaperTrading.Domain;

namespace PaperTrading.Domain.Tests;

public sealed class MarketOrderTests : EngineTestBase
{
    [Fact]
    public void Market_buy_fills_at_last_price_and_debits_exact_cost()
    {
        Tick(50_000.12m, Btc);

        var order = Market(OrderSide.Buy, 0.12345m, Btc);

        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(50_000.12m, order.FillPrice);
        Assert.Equal(6_172.514814m, order.Notional);
        Assert.Equal(StartingCash - 6_172.514814m, Account.Cash);

        var position = Account.FindPosition(Btc)!;
        Assert.Equal(0.12345m, position.Quantity);
        Assert.Equal(6_172.514814m, position.CostBasis);
    }

    [Fact]
    public void Market_order_is_rejected_when_no_price_has_been_received()
    {
        var order = Market(OrderSide.Buy, 1m);

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal(RejectReasons.NoMarketPrice, order.RejectReason);
        Assert.Equal(StartingCash, Account.Cash);
    }

    [Fact]
    public void Market_buy_above_available_cash_is_rejected_and_changes_nothing()
    {
        Tick(3_000m);

        var order = Market(OrderSide.Buy, 34m); // 102,000 > 100,000

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal(RejectReasons.InsufficientFunds, order.RejectReason);
        Assert.Equal(StartingCash, Account.Cash);
        Assert.Null(Account.FindPosition(Eth));
    }

    [Fact]
    public void Market_buy_can_spend_exactly_all_available_cash()
    {
        Tick(2_500m);

        var order = Market(OrderSide.Buy, 40m); // exactly 100,000

        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(0m, Account.Cash);
    }

    [Fact]
    public void Market_sell_credits_proceeds_and_realizes_pnl_against_average_cost()
    {
        Tick(100m);
        Market(OrderSide.Buy, 1m);
        Tick(150m);

        var sell = Market(OrderSide.Sell, 0.4m);

        Assert.Equal(OrderStatus.Filled, sell.Status);
        Assert.Equal(20m, Account.RealizedPnl); // 0.4 × (150 − 100)
        Assert.Equal(StartingCash - 100m + 60m, Account.Cash);
        Assert.Equal(0.6m, Account.FindPosition(Eth)!.Quantity);
        Assert.Equal(60m, Account.FindPosition(Eth)!.CostBasis);
    }

    [Fact]
    public void Market_sell_of_more_than_held_is_rejected()
    {
        Tick(100m);
        Market(OrderSide.Buy, 1m);

        var sell = Market(OrderSide.Sell, 1.0001m);

        Assert.Equal(OrderStatus.Rejected, sell.Status);
        Assert.Equal(RejectReasons.InsufficientPosition, sell.RejectReason);
        Assert.Equal(1m, Account.FindPosition(Eth)!.Quantity);
    }

    [Fact]
    public void Market_sell_without_any_position_is_rejected()
    {
        Tick(100m);

        var sell = Market(OrderSide.Sell, 1m);

        Assert.Equal(RejectReasons.InsufficientPosition, sell.RejectReason);
    }

    [Fact]
    public void Market_sell_cannot_use_quantity_reserved_by_a_resting_limit_sell()
    {
        Tick(100m);
        Market(OrderSide.Buy, 1m);
        Limit(OrderSide.Sell, 0.7m, 200m);

        var sell = Market(OrderSide.Sell, 0.5m); // only 0.3 is free

        Assert.Equal(RejectReasons.InsufficientPosition, sell.RejectReason);
    }

    [Fact]
    public void Many_small_trades_accumulate_without_floating_point_drift()
    {
        Tick(0.10m);

        for (var i = 0; i < 1_000; i++)
        {
            Market(OrderSide.Buy, 0.0001m);
        }

        // 1,000 × 0.0001 × 0.10 = 0.01 exactly. With double this would not be exact.
        Assert.Equal(StartingCash - 0.01m, Account.Cash);
        Assert.Equal(0.1m, Account.FindPosition(Eth)!.Quantity);
        Assert.Equal(0.01m, Account.FindPosition(Eth)!.CostBasis);
    }
}
