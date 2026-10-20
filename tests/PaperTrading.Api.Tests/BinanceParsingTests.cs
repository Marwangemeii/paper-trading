using PaperTrading.Api.Feed;

namespace PaperTrading.Api.Tests;

public sealed class BinanceParsingTests
{
    [Fact]
    public void A_mini_ticker_message_becomes_an_exact_normalized_price_tick()
    {
        const string json = """{"stream":"btcusdt@miniTicker","data":{"e":"24hrMiniTicker","E":1700000000000,"s":"BTCUSDT","c":"67010.01000000","o":"66000.00"}}""";

        var parsed = BinancePriceFeed.TryParse(json, out var tick);

        Assert.True(parsed);
        Assert.Equal("BTCUSDT", tick.Symbol);
        Assert.Equal(67010.01m, tick.Price);
        Assert.Equal("67010.01", tick.Price.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000), tick.At);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"result":null,"id":1}""")]
    [InlineData("""{"data":{"E":1,"s":"BTCUSDT","c":"abc"}}""")]
    [InlineData("""{"data":{"E":1,"s":"BTCUSDT","c":"0"}}""")]
    public void Unexpected_messages_are_ignored(string json)
    {
        Assert.False(BinancePriceFeed.TryParse(json, out _));
    }
}
