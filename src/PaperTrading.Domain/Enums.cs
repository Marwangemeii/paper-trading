namespace PaperTrading.Domain;

public enum OrderSide
{
    Buy,
    Sell,
}

public enum OrderType
{
    Market,
    Limit,
}

public enum OrderStatus
{
    /// <summary>Resting limit order waiting for the price to reach it.</summary>
    Open,
    Filled,
    Cancelled,
    /// <summary>Accepted for processing but refused by a business rule (e.g. insufficient funds).</summary>
    Rejected,
}
