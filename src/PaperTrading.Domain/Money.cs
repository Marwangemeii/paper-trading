namespace PaperTrading.Domain;

/// <summary>
/// Monetary conventions. All amounts are <see cref="decimal"/> in the quote currency (USDT).
/// Multiplication of a quantity by a price is exact in decimal, so we only round where a
/// division happens (allocating cost basis on a partial sell, average cost for display).
/// </summary>
public static class Money
{
    public const string QuoteCurrency = "USDT";

    /// <summary>Number of decimal places kept when a division forces rounding.</summary>
    public const int Scale = 8;

    /// <summary>Banker's rounding avoids a systematic upward bias over many operations.</summary>
    public static decimal Round(decimal value) => decimal.Round(value, Scale, MidpointRounding.ToEven);

    public static bool HasValidScale(decimal value) => decimal.Round(value, Scale) == value;
}
