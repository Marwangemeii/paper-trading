namespace PaperTrading.Domain;

/// <summary>
/// Resting limit orders for one symbol, kept sorted so that a price tick only touches the
/// orders it actually triggers: buys by highest limit first, sells by lowest limit first,
/// ties broken by arrival order (price-time priority).
/// </summary>
internal sealed class OrderBook
{
    private static readonly Comparer<Order> BuyPriority = Comparer<Order>.Create((a, b) =>
    {
        var byPrice = b.LimitPrice!.Value.CompareTo(a.LimitPrice!.Value);
        return byPrice != 0 ? byPrice : a.Sequence.CompareTo(b.Sequence);
    });

    private static readonly Comparer<Order> SellPriority = Comparer<Order>.Create((a, b) =>
    {
        var byPrice = a.LimitPrice!.Value.CompareTo(b.LimitPrice!.Value);
        return byPrice != 0 ? byPrice : a.Sequence.CompareTo(b.Sequence);
    });

    private readonly SortedSet<Order> _buys = new(BuyPriority);
    private readonly SortedSet<Order> _sells = new(SellPriority);

    public int Count => _buys.Count + _sells.Count;

    public void Add(Order order) => SideOf(order).Add(order);

    public bool Remove(Order order) => SideOf(order).Remove(order);

    /// <summary>Removes and returns every order triggered by <paramref name="price"/>, in arrival order.</summary>
    public List<Order> TakeTriggered(decimal price)
    {
        var triggered = new List<Order>();
        TakeWhileTriggered(_buys, price, triggered);
        TakeWhileTriggered(_sells, price, triggered);
        triggered.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
        return triggered;
    }

    private static void TakeWhileTriggered(SortedSet<Order> side, decimal price, List<Order> into)
    {
        while (side.Min is { } best && best.IsTriggeredBy(price))
        {
            side.Remove(best);
            into.Add(best);
        }
    }

    private SortedSet<Order> SideOf(Order order) => order.Side == OrderSide.Buy ? _buys : _sells;
}
