using System.Net;
using System.Text.Json;
using static PaperTrading.Api.Tests.ApiClientExtensions;

namespace PaperTrading.Api.Tests;

public sealed class OrderApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        _factory.DeleteDatabase();
    }

    [Fact]
    public async Task A_new_account_starts_with_the_configured_cash_as_an_exact_string()
    {
        var client = _factory.CreateClient();
        var accountId = await client.CreateAccountAsync();

        var portfolio = await client.GetJsonAsync($"/api/accounts/{accountId}/portfolio");

        Assert.Equal(JsonValueKind.String, portfolio.GetProperty("cash").ValueKind);
        Assert.Equal("100000", portfolio.GetProperty("cash").GetString());
    }

    [Fact]
    public async Task Market_order_is_created_and_filled_at_the_last_price()
    {
        var client = _factory.CreateClient();
        var accountId = await client.CreateAccountAsync();
        await _factory.SetPriceAsync("BTCUSDT", 50000.12m);

        var response = await client.PlaceOrderAsync(accountId, "k1", MarketBuy("BTCUSDT", "0.12345"));
        var order = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Filled", order.GetProperty("status").GetString());
        Assert.Equal("50000.12", order.GetProperty("fillPrice").GetString());
        Assert.Equal("6172.514814", order.GetProperty("notional").GetString());
    }

    [Fact]
    public async Task Retry_returns_the_original_order_with_200_and_a_replay_header()
    {
        var client = _factory.CreateClient();
        var accountId = await client.CreateAccountAsync();
        await _factory.SetPriceAsync("ETHUSDT", 100m);

        var first = await client.PlaceOrderAsync(accountId, "same", MarketBuy("ETHUSDT", "1"));
        var retry = await client.PlaceOrderAsync(accountId, "same", MarketBuy("ETHUSDT", "1"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.True(retry.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(
            (await first.ReadJsonAsync()).GetProperty("id").GetGuid(),
            (await retry.ReadJsonAsync()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_order_returns_409()
    {
        var client = _factory.CreateClient();
        var accountId = await client.CreateAccountAsync();
        await _factory.SetPriceAsync("ETHUSDT", 100m);
        await client.PlaceOrderAsync(accountId, "same", MarketBuy("ETHUSDT", "1"));

        var conflict = await client.PlaceOrderAsync(accountId, "same", MarketBuy("ETHUSDT", "2"));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Missing_idempotency_key_returns_400()
    {
        var client = _factory.CreateClient();
        var accountId = await client.CreateAccountAsync();

        var response = await client.PlaceOrderAsync(accountId, null, MarketBuy("ETHUSDT", "1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("ETHUSDT", "0")]
    [InlineData("ETHUSDT", "0.00001")]
    [InlineData("DOGEUSDT", "1")]
    public async Task Invalid_orders_return_400_and_are_not_recorded(string symbol, string quantity)
    {
        var client = _factory.CreateClient();
        var accountId = await client.CreateAccountAsync();

        var response = await client.PlaceOrderAsync(accountId, "k", MarketBuy(symbol, quantity));
        var history = await client.GetJsonAsync($"/api/accounts/{accountId}/orders");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, history.GetArrayLength());
    }

    [Fact]
    public async Task Unknown_account_returns_404()
    {
        var client = _factory.CreateClient();

        var response = await client.PlaceOrderAsync(Guid.NewGuid(), "k", MarketBuy("ETHUSDT", "1"));
        var portfolio = await client.GetAsync(new Uri($"/api/accounts/{Guid.NewGuid()}/portfolio", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, portfolio.StatusCode);
    }

    [Fact]
    public async Task Cancel_releases_a_limit_order_once_and_then_returns_409()
    {
        var client = _factory.CreateClient();
        var accountId = await client.CreateAccountAsync();
        await _factory.SetPriceAsync("ETHUSDT", 100m);
        var placed = await (await client.PlaceOrderAsync(accountId, "k", LimitBuy("ETHUSDT", "2", "90"))).ReadJsonAsync();
        var orderUrl = new Uri($"/api/accounts/{accountId}/orders/{placed.GetProperty("id").GetGuid()}", UriKind.Relative);

        var first = await client.DeleteAsync(orderUrl);
        var second = await client.DeleteAsync(orderUrl);
        var portfolio = await client.GetJsonAsync($"/api/accounts/{accountId}/portfolio");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("Cancelled", (await first.ReadJsonAsync()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("0", portfolio.GetProperty("reservedCash").GetString());
    }

    [Fact]
    public async Task Order_history_is_newest_first()
    {
        var client = _factory.CreateClient();
        var accountId = await client.CreateAccountAsync();
        await _factory.SetPriceAsync("ETHUSDT", 100m);
        await client.PlaceOrderAsync(accountId, "first", MarketBuy("ETHUSDT", "1"));
        await client.PlaceOrderAsync(accountId, "second", MarketBuy("ETHUSDT", "2"));

        var history = await client.GetJsonAsync($"/api/accounts/{accountId}/orders");

        Assert.Equal("2", history[0].GetProperty("quantity").GetString());
        Assert.Equal("1", history[1].GetProperty("quantity").GetString());
    }

    [Fact]
    public async Task State_survives_a_restart_and_resting_orders_still_fill()
    {
        Guid accountId;
        Guid orderId;
        using (var before = new ApiFactory(_factory.DatabasePath))
        {
            var client = before.CreateClient();
            accountId = await client.CreateAccountAsync();
            await before.SetPriceAsync("ETHUSDT", 100m);
            await client.PlaceOrderAsync(accountId, "bought", MarketBuy("ETHUSDT", "1"));
            var resting = await (await client.PlaceOrderAsync(accountId, "resting", LimitBuy("ETHUSDT", "2", "90"))).ReadJsonAsync();
            orderId = resting.GetProperty("id").GetGuid();
        }

        var restartedClient = _factory.CreateClient();
        await _factory.SetPriceAsync("ETHUSDT", 89m);

        var order = await restartedClient.GetJsonAsync($"/api/accounts/{accountId}/orders/{orderId}");
        var portfolio = await restartedClient.GetJsonAsync($"/api/accounts/{accountId}/portfolio");
        var replay = await restartedClient.PlaceOrderAsync(accountId, "bought", MarketBuy("ETHUSDT", "1"));

        Assert.Equal("Filled", order.GetProperty("status").GetString());
        Assert.Equal("89", order.GetProperty("fillPrice").GetString());
        Assert.Equal("3", portfolio.GetProperty("positions")[0].GetProperty("quantity").GetString());
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
    }
}
