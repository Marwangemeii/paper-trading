using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;

namespace PaperTrading.Api.Feed;

/// <summary>
/// Generates prices locally: a random walk of up to ±0.1% per step from each instrument's
/// configured start price, rounded to its tick size. Used when Binance is unreachable.
/// </summary>
public sealed class MockPriceFeed(
    IOptions<TradingOptions> trading,
    IOptions<PriceFeedOptions> feed,
    TimeProvider time)
{
    public async IAsyncEnumerable<PriceTick> StreamAsync(
        IReadOnlyDictionary<string, decimal> startPrices,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var instruments = trading.Value.Instruments;
        var prices = instruments.ToDictionary(
            i => i.Symbol,
            i => startPrices.GetValueOrDefault(i.Symbol, i.MockStartPrice));

        using var timer = new PeriodicTimer(feed.Value.MockInterval, time);
        while (await timer.WaitForNextTickAsync(ct))
        {
            foreach (var instrument in instruments)
            {
                // Random is fine here: this only simulates market noise, it never touches balances.
#pragma warning disable CA5394
                var change = (decimal)(Random.Shared.NextDouble() * 0.002 - 0.001);
#pragma warning restore CA5394
                var next = decimal.Round(prices[instrument.Symbol] * (1 + change) / instrument.TickSize) * instrument.TickSize;
                prices[instrument.Symbol] = Math.Max(instrument.TickSize, next);

                yield return new PriceTick(instrument.Symbol, prices[instrument.Symbol], time.GetUtcNow());
            }
        }
    }
}
