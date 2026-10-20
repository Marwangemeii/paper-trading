using Microsoft.AspNetCore.SignalR;

namespace PaperTrading.Api.Realtime;

/// <summary>
/// Real-time channel to browsers. Server → client events: "price" (to everyone) and
/// "orderUpdated" (only to the owning account's group, e.g. when a resting limit order fills).
/// </summary>
public sealed class MarketHub : Hub
{
    public const string Path = "/hubs/market";
    public const string PriceEvent = "price";
    public const string OrderUpdatedEvent = "orderUpdated";

    public static string AccountGroup(Guid accountId) => $"account:{accountId}";

    /// <summary>Subscribes this connection to its account's private order updates.</summary>
    public Task JoinAccount(Guid accountId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, AccountGroup(accountId));
}
