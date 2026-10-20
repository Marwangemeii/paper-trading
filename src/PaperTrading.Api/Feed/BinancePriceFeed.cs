using System.Globalization;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PaperTrading.Api.Feed;

/// <summary>
/// Streams live prices from Binance's public market-data WebSocket (no API key needed).
/// Subscribes to the 1-second "miniTicker" stream of every configured symbol and uses the
/// last traded price ("c"). The data-stream.binance.vision host serves market data only.
/// </summary>
public sealed class BinancePriceFeed(IOptions<TradingOptions> trading, IOptions<PriceFeedOptions> feed)
{
    /// <summary>No message for this long means the connection is dead even if the socket looks open.</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(15);

    public async IAsyncEnumerable<PriceTick> StreamAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var streams = string.Join('/', trading.Value.Instruments.Select(i => $"{i.Symbol.ToLowerInvariant()}@miniTicker"));
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"{feed.Value.BinanceUrl}?streams={streams}"), ct);

        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (socket.State == WebSocketState.Open)
        {
            using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
            stall.CancelAfter(StallTimeout);

            message.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, stall.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    yield break;
                }

                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (TryParse(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length), out var tick))
            {
                yield return tick;
            }
        }
    }

    /// <summary>
    /// Parses a combined-stream message: {"stream":"btcusdt@miniTicker","data":{"E":1700000000000,"s":"BTCUSDT","c":"67010.01000000",...}}.
    /// </summary>
    public static bool TryParse(string json, out PriceTick tick)
    {
        tick = default!;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("s", out var symbol)
                || !data.TryGetProperty("c", out var close)
                || !data.TryGetProperty("E", out var eventTime)
                || !decimal.TryParse(close.GetString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price)
                || price <= 0)
            {
                return false;
            }

            tick = new PriceTick(
                symbol.GetString()!,
                DecimalStringJsonConverter.Normalize(price),
                DateTimeOffset.FromUnixTimeMilliseconds(eventTime.GetInt64()));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
