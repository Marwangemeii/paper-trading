using PaperTrading.Domain;

namespace PaperTrading.Domain.Tests;

public sealed class PortfolioTests : EngineTestBase
{
    [Fact]
    public void Average_cost_is_weighted_by_quantity()
    {
        Tick(100m);
        Market(OrderSide.Buy, 1m);
        Tick(200m);
        Market(OrderSide.Buy, 3m);

        var position = Account.FindPosition(Eth)!;

        Assert.Equal(4m, position.Quantity);
        Assert.Equal(700m, position.CostBasis);
        Assert.Equal(175m, position.AverageCost);
    }

    [Fact]
    public void Partial_sell_realizes_pnl_against_average_cost_and_keeps_it_for_the_rest()
    {
        Tick(100m);
        Market(OrderSide.Buy, 1m);
        Tick(200m);
        Market(OrderSide.Buy, 3m);
        Tick(300m);

        Market(OrderSide.Sell, 1m);

        var position = Account.FindPosition(Eth)!;
        Assert.Equal(125m, Account.RealizedPnl); // 300 − 175
        Assert.Equal(125m, position.RealizedPnl);
        Assert.Equal(525m, position.CostBasis);
        Assert.Equal(175m, position.AverageCost);
    }

    [Fact]
    public void Rounding_on_partial_sells_never_creates_or_loses_money()
    {
        // Cost basis 100 for 3 units: each third is 33.333… and must be rounded.
        Tick(33.33m);
        Market(OrderSide.Buy, 2m);
        Tick(33.34m);
        Market(OrderSide.Buy, 1m);
        Assert.Equal(100m, Account.FindPosition(Eth)!.CostBasis);

        Tick(50m);
        Market(OrderSide.Sell, 1m);
        Market(OrderSide.Sell, 1m);
        Market(OrderSide.Sell, 1m);

        // Whatever the rounding of each slice, the total P&L is exactly proceeds − cost.
        Assert.Equal(150m - 100m, Account.RealizedPnl);
        Assert.Equal(0m, Account.FindPosition(Eth)!.CostBasis);
        Assert.Equal(StartingCash + 50m, Account.Cash);
    }

    [Fact]
    public void Closing_a_position_resets_cost_basis_and_average_cost()
    {
        Tick(100m);
        Market(OrderSide.Buy, 2m);

        Market(OrderSide.Sell, 2m);

        var position = Account.FindPosition(Eth)!;
        Assert.Equal(0m, position.Quantity);
        Assert.Equal(0m, position.CostBasis);
        Assert.Equal(0m, position.AverageCost);
    }

    [Fact]
    public void Snapshot_values_positions_at_the_last_price()
    {
        Tick(100m);
        Market(OrderSide.Buy, 2m);
        Tick(130m);

        var snapshot = Engine.GetPortfolio(AccountId)!;

        var position = Assert.Single(snapshot.Positions);
        Assert.Equal(130m, position.MarketPrice);
        Assert.Equal(260m, position.MarketValue);
        Assert.Equal(60m, position.UnrealizedPnl);
        Assert.Equal(60m, snapshot.UnrealizedPnl);
        Assert.Equal(StartingCash - 200m, snapshot.Cash);
        Assert.Equal(StartingCash + 60m, snapshot.Equity);
    }

    [Fact]
    public void Position_without_a_price_is_valued_at_cost()
    {
        Tick(100m);
        Market(OrderSide.Buy, 2m);

        var snapshot = Account.Valuate(_ => null);

        Assert.Equal(200m, snapshot.MarketValue);
        Assert.Equal(0m, snapshot.UnrealizedPnl);
        Assert.Equal(StartingCash, snapshot.Equity);
    }

    [Fact]
    public void Accounting_invariants_hold_through_a_long_random_trading_session()
    {
        // Deterministic pseudo-random walk: mixed order types, sides, cancels and price moves.
        var random = new Random(42);
        var open = new List<Order>();
        var price = 100m;
        Tick(price);

        for (var step = 0; step < 2_000; step++)
        {
            var quantity = random.Next(1, 20_000) * 0.0001m; // up to 2 ETH
            var limit = random.Next(8_000, 12_000) / 100m;   // 80.00 – 119.99
            var side = random.Next(2) == 0 ? OrderSide.Buy : OrderSide.Sell;

            switch (random.Next(4))
            {
                case 0:
                    Market(side, quantity);
                    break;
                case 1:
                    var order = Limit(side, quantity, limit);
                    if (order.Status == OrderStatus.Open)
                    {
                        open.Add(order);
                    }

                    break;
                case 2 when open.Count > 0:
                    var victim = open[random.Next(open.Count)];
                    Engine.CancelOrder(AccountId, victim.Id);
                    break;
                default:
                    price = Math.Max(0.01m, price + random.Next(-300, 301) / 100m);
                    Tick(price);
                    break;
            }

            open.RemoveAll(o => o.Status != OrderStatus.Open);
            AssertInvariants(open);
        }
    }

    private void AssertInvariants(List<Order> openOrders)
    {
        var account = Account;
        var snapshot = Engine.GetPortfolio(AccountId)!;

        Assert.True(account.Cash >= 0, "cash went negative");
        Assert.True(account.AvailableCash >= 0, "reserved more cash than held");
        Assert.Equal(openOrders.Sum(o => o.ReservedCash), account.ReservedCash);

        foreach (var position in account.Positions)
        {
            Assert.True(position.Quantity >= 0);
            Assert.True(position.AvailableQuantity >= 0);
            Assert.Equal(
                openOrders.Where(o => o.Symbol == position.Symbol).Sum(o => o.ReservedQuantity),
                position.ReservedQuantity);
        }

        // Every unit of profit or loss is accounted for: no money created or destroyed.
        Assert.Equal(snapshot.Equity - account.InitialCash, snapshot.RealizedPnl + snapshot.UnrealizedPnl);
    }
}
