using PaperTrading.Domain;

namespace PaperTrading.Domain.Tests;

public sealed class ValidationTests : EngineTestBase
{
    public static TheoryData<string, OrderType, decimal, decimal?> InvalidOrders => new()
    {
        { "DOGEUSDT", OrderType.Market, 1m, null },   // unknown symbol
        { Eth, OrderType.Market, 0m, null },          // zero quantity
        { Eth, OrderType.Market, -1m, null },         // negative quantity
        { Eth, OrderType.Market, 0.00001m, null },    // finer than the 0.0001 step
        { Eth, OrderType.Market, 1m, 100m },          // market order with a limit price
        { Eth, OrderType.Limit, 1m, null },           // limit order without a price
        { Eth, OrderType.Limit, 1m, 0m },             // zero limit price
        { Eth, OrderType.Limit, 1m, -5m },            // negative limit price
        { Eth, OrderType.Limit, 1m, 100.001m },       // finer than the 0.01 tick
        { Eth, (OrderType)99, 1m, null },             // undefined enum value
    };

    [Theory]
    [MemberData(nameof(InvalidOrders))]
    public void Invalid_orders_are_refused_without_being_recorded(
        string symbol, OrderType type, decimal quantity, decimal? limitPrice)
    {
        Tick(100m);

        var result = Place(OrderSide.Buy, type, quantity, limitPrice, symbol);

        Assert.Equal(PlaceOrderOutcome.Invalid, result.Outcome);
        Assert.Null(result.Order);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(StartingCash, Account.AvailableCash);
    }

    [Fact]
    public void Trailing_zeros_do_not_make_a_valid_quantity_invalid()
    {
        Tick(100m);

        var order = Market(OrderSide.Buy, 1.50000000m);

        Assert.Equal(OrderStatus.Filled, order.Status);
    }

    [Fact]
    public void Orders_for_an_unknown_account_are_refused()
    {
        var result = Place(OrderSide.Buy, OrderType.Market, 1m, accountId: Guid.NewGuid());

        Assert.Equal(PlaceOrderOutcome.AccountNotFound, result.Outcome);
    }

    [Fact]
    public void Opening_the_same_account_twice_throws()
    {
        Assert.Throws<InvalidOperationException>(() => Engine.OpenAccount(AccountId, 1m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Accounts_need_positive_starting_cash(decimal initialCash)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Engine.OpenAccount(Guid.NewGuid(), initialCash));
    }

    [Fact]
    public void Price_ticks_must_be_positive_and_for_a_known_symbol()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Tick(0m));
        Assert.Throws<ArgumentException>(() => Tick(1m, "DOGEUSDT"));
    }
}
