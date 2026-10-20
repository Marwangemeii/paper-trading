namespace PaperTrading.Domain;

/// <summary>
/// A tradable pair, e.g. BTCUSDT. <see cref="QuantityStep"/> and <see cref="TickSize"/> mirror
/// exchange lot-size and price filters: we reject values that are not exact multiples instead
/// of silently rounding the user's input.
/// </summary>
public sealed record Instrument(string Symbol, string BaseAsset, decimal QuantityStep, decimal TickSize)
{
    public bool IsValidQuantity(decimal quantity) => quantity > 0 && quantity % QuantityStep == 0;

    public bool IsValidPrice(decimal price) => price > 0 && price % TickSize == 0;
}
