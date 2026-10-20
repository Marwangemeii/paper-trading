namespace PaperTrading.Domain;

/// <summary>
/// Holdings of one instrument, valued with the average-cost method. We store the total
/// <see cref="CostBasis"/> (exact) rather than the average price (rounded), so no money is lost
/// to rounding: the full cost basis always leaves the position when it is closed.
/// </summary>
public sealed class Position
{
    // Required by EF Core for materialisation.
    private Position() { }

    internal Position(Guid accountId, string symbol)
    {
        AccountId = accountId;
        Symbol = symbol;
    }

    public Guid AccountId { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }

    /// <summary>Part of <see cref="Quantity"/> promised to resting limit sell orders.</summary>
    public decimal ReservedQuantity { get; private set; }

    /// <summary>Total amount paid for the quantity currently held.</summary>
    public decimal CostBasis { get; private set; }

    public decimal RealizedPnl { get; private set; }

    public decimal AvailableQuantity => Quantity - ReservedQuantity;

    /// <summary>Display value only; calculations use <see cref="CostBasis"/>.</summary>
    public decimal AverageCost => Quantity == 0 ? 0m : Money.Round(CostBasis / Quantity);

    public decimal UnrealizedPnl(decimal marketPrice) => Quantity * marketPrice - CostBasis;

    internal void Add(decimal quantity, decimal cost)
    {
        Quantity += quantity;
        CostBasis += cost;
    }

    /// <summary>Removes quantity and returns the share of cost basis that left with it.</summary>
    internal decimal Remove(decimal quantity)
    {
        if (quantity > AvailableQuantity)
        {
            throw new InvalidOperationException(
                $"Cannot remove {quantity} {Symbol}; only {AvailableQuantity} available.");
        }

        // Closing the whole position takes the whole cost basis, so rounding residue never lingers.
        var costRemoved = quantity == Quantity
            ? CostBasis
            : Money.Round(CostBasis * quantity / Quantity);

        Quantity -= quantity;
        CostBasis -= costRemoved;
        return costRemoved;
    }

    internal void Reserve(decimal quantity)
    {
        if (quantity > AvailableQuantity)
        {
            throw new InvalidOperationException(
                $"Cannot reserve {quantity} {Symbol}; only {AvailableQuantity} available.");
        }

        ReservedQuantity += quantity;
    }

    internal void Release(decimal quantity)
    {
        if (quantity > ReservedQuantity)
        {
            throw new InvalidOperationException(
                $"Cannot release {quantity} {Symbol}; only {ReservedQuantity} reserved.");
        }

        ReservedQuantity -= quantity;
    }

    internal void AddRealizedPnl(decimal pnl) => RealizedPnl += pnl;
}
