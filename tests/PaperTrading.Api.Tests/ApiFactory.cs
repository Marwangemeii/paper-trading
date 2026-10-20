using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PaperTrading.Api;
using PaperTrading.Api.Engine;

namespace PaperTrading.Api.Tests;

/// <summary>
/// Runs the real API in memory with its own SQLite file and no live feed; tests push prices
/// themselves. Pass the same <paramref name="databasePath"/> to a second factory to simulate a restart.
/// </summary>
public sealed class ApiFactory(string? databasePath = null) : WebApplicationFactory<Program>
{
    public string DatabasePath { get; } = databasePath ?? Path.Combine(Path.GetTempPath(), $"paper-trading-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Trading", $"Data Source={DatabasePath}");
        builder.UseSetting("PriceFeed:Mode", "None");
    }

    public Task SetPriceAsync(string symbol, decimal price) =>
        Services.GetRequiredService<EngineHost>()
            .ApplyPriceTickAsync(new PriceTick(symbol, price, DateTimeOffset.UtcNow), CancellationToken.None);

    public void DeleteDatabase()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            File.Delete(DatabasePath + suffix);
        }
    }
}

internal static class ApiClientExtensions
{
    public static async Task<Guid> CreateAccountAsync(this HttpClient client)
    {
        var response = await client.PostAsync(new Uri("/api/accounts", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accountId").GetGuid();
    }

    public static Task<HttpResponseMessage> PlaceOrderAsync(
        this HttpClient client, Guid accountId, string? idempotencyKey, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts/{accountId}/orders")
        {
            Content = JsonContent.Create(body),
        };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(request);
    }

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(new Uri(url, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<JsonElement>();

    public static object MarketBuy(string symbol, string quantity) =>
        new { symbol, side = "Buy", type = "Market", quantity };

    public static object LimitBuy(string symbol, string quantity, string limitPrice) =>
        new { symbol, side = "Buy", type = "Limit", quantity, limitPrice };
}
