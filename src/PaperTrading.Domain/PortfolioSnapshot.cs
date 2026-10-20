namespace PaperTrading.Domain;

/// <summary>Point-in-time valuation of an account. Equity = Cash + MarketValue.</summary>
public sealed record PortfolioSnapshot(
    Guid AccountId,
    decimal InitialCash,
    decimal Cash,
    decimal ReservedCash,
    decimal AvailableCash,
    decimal MarketValue,
    decimal UnrealizedPnl,
    decimal RealizedPnl,
    decimal Equity,
    IReadOnlyList<PositionSnapshot> Positions);

public sealed record PositionSnapshot(
    string Symbol,
    decimal Quantity,
    decimal ReservedQuantity,
    decimal AverageCost,
    decimal CostBasis,
    decimal? MarketPrice,
    decimal MarketValue,
    decimal UnrealizedPnl,
    decimal RealizedPnl);
