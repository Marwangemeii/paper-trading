namespace PaperTrading.Domain;

/// <summary>
/// Stable machine-readable codes stored on rejected orders. The frontend translates them,
/// so they must not change once released.
/// </summary>
public static class RejectReasons
{
    public const string NoMarketPrice = "NO_MARKET_PRICE";
    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";
    public const string InsufficientPosition = "INSUFFICIENT_POSITION";
}
