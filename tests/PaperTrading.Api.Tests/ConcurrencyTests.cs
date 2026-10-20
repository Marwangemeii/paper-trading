using System.Net;
using static PaperTrading.Api.Tests.ApiClientExtensions;

namespace PaperTrading.Api.Tests;

public sealed class ConcurrencyTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        _factory.DeleteDatabase();
    }

    [Fact]
    public async Task Parallel_orders_can_never_spend_more_cash_than_the_account_has()
    {
        var client = _factory.CreateClient();
        var accountId = await client.CreateAccountAsync();
        await _factory.SetPriceAsync("ETHUSDT", 100m);

        // 200 orders × 1,000 USDT at the same moment against 100,000 USDT of cash.
        var responses = await Task.WhenAll(Enumerable.Range(0, 200).Select(i =>
            client.PlaceOrderAsync(accountId, $"order-{i}", MarketBuy("ETHUSDT", "10"))));

        var statuses = await Task.WhenAll(responses.Select(async r =>
            (await r.ReadJsonAsync()).GetProperty("status").GetString()));
        var portfolio = await client.GetJsonAsync($"/api/accounts/{accountId}/portfolio");

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(100, statuses.Count(s => s == "Filled"));
        Assert.Equal(100, statuses.Count(s => s == "Rejected"));
        Assert.Equal("0", portfolio.GetProperty("cash").GetString());
        Assert.Equal("1000", portfolio.GetProperty("positions")[0].GetProperty("quantity").GetString());
    }

    [Fact]
    public async Task Parallel_retries_with_the_same_key_create_exactly_one_order()
    {
        var client = _factory.CreateClient();
        var accountId = await client.CreateAccountAsync();
        await _factory.SetPriceAsync("ETHUSDT", 100m);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            client.PlaceOrderAsync(accountId, "double-click", MarketBuy("ETHUSDT", "1"))));

        var orderIds = await Task.WhenAll(responses.Select(async r =>
            (await r.ReadJsonAsync()).GetProperty("id").GetGuid()));
        var history = await client.GetJsonAsync($"/api/accounts/{accountId}/orders");
        var portfolio = await client.GetJsonAsync($"/api/accounts/{accountId}/portfolio");

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Single(orderIds.Distinct());
        Assert.Equal(1, history.GetArrayLength());
        Assert.Equal("99900", portfolio.GetProperty("cash").GetString());
    }
}
