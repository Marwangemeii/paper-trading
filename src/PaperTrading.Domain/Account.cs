namespace PaperTrading.Domain;

/// <summary>
/// A virtual trading account: cash in the quote currency plus positions.
/// Invariants: cash and reserved cash never go negative, and reserved cash never exceeds cash.
/// The matching engine checks business rules first; these guards are a last line of defence.
/// </summary>
public sealed class Account
{
    private readonly List<Position> _positions = [];

    // Required by EF Core for materialisation.
    private Account() { }

    public Guid Id { get; private set; }
    public decimal InitialCash { get; private set; }

    /// <summary>Total cash, including the part reserved for resting limit buys.</summary>
    public decimal Cash { get; private set; }

    public decimal ReservedCash { get; private set; }
    public decimal RealizedPnl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public decimal AvailableCash => Cash - ReservedCash;
    public IReadOnlyCollection<Position> Positions => _positions;

    public static Account Open(Guid id, decimal initialCash, DateTimeOffset now)
    {
        if (initialCash <= 0 || !Money.HasValidScale(initialCash))
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialCash), initialCash, $"Must be positive with at most {Money.Scale} decimals.");
        }

        return new Account { Id = id, InitialCash = initialCash, Cash = initialCash, CreatedAt = now };
    }

    public Position? FindPosition(string symbol) => _positions.Find(p => p.Symbol == symbol);

    internal void ReserveCash(decimal amount)
    {
        if (amount > AvailableCash)
        {
            throw new InvalidOperationException($"Cannot reserve {amount}; only {AvailableCash} available.");
        }

        ReservedCash += amount;
    }

    internal void ReleaseCash(decimal amount)
    {
        if (amount > ReservedCash)
        {
            throw new InvalidOperationException($"Cannot release {amount}; only {ReservedCash} reserved.");
        }

        ReservedCash -= amount;
    }

    internal void ReserveQuantity(string symbol, decimal quantity) => RequirePosition(symbol).Reserve(quantity);

    internal void ReleaseQuantity(string symbol, decimal quantity) => RequirePosition(symbol).Release(quantity);

    internal void SettleBuy(string symbol, decimal quantity, decimal price)
    {
        var cost = quantity * price;
        if (cost > AvailableCash)
        {
            throw new InvalidOperationException($"Buy costs {cost}; only {AvailableCash} available.");
        }

        Cash -= cost;
        GetOrAddPosition(symbol).Add(quantity, cost);
    }

    internal void SettleSell(string symbol, decimal quantity, decimal price)
    {
        var position = RequirePosition(symbol);
        var costRemoved = position.Remove(quantity);
        var proceeds = quantity * price;
        var pnl = proceeds - costRemoved;

        Cash += proceeds;
        RealizedPnl += pnl;
        position.AddRealizedPnl(pnl);
    }

    /// <summary>
    /// Values the portfolio at current prices. A position with no known price is valued at
    /// cost (zero unrealized P&amp;L) rather than at zero, so equity does not collapse on startup.
    /// </summary>
    public PortfolioSnapshot Valuate(Func<string, decimal?> priceOf)
    {
        ArgumentNullException.ThrowIfNull(priceOf);

        var positions = _positions
            .OrderBy(p => p.Symbol, StringComparer.Ordinal)
            .Select(p =>
            {
                var price = priceOf(p.Symbol);
                var marketValue = price is { } px ? p.Quantity * px : p.CostBasis;
                return new PositionSnapshot(
                    p.Symbol,
                    p.Quantity,
                    p.ReservedQuantity,
                    p.AverageCost,
                    p.CostBasis,
                    price,
                    marketValue,
                    marketValue - p.CostBasis,
                    p.RealizedPnl);
            })
            .ToList();

        var marketValueTotal = positions.Sum(p => p.MarketValue);
        var unrealizedTotal = positions.Sum(p => p.UnrealizedPnl);

        return new PortfolioSnapshot(
            Id,
            InitialCash,
            Cash,
            ReservedCash,
            AvailableCash,
            marketValueTotal,
            unrealizedTotal,
            RealizedPnl,
            Cash + marketValueTotal,
            positions);
    }

    private Position RequirePosition(string symbol) =>
        FindPosition(symbol) ?? throw new InvalidOperationException($"No position in {symbol}.");

    private Position GetOrAddPosition(string symbol)
    {
        var position = FindPosition(symbol);
        if (position is null)
        {
            position = new Position(Id, symbol);
            _positions.Add(position);
        }

        return position;
    }
}
