using PaperTrading.Domain;

namespace PaperTrading.Api;

public sealed class TradingOptions
{
    public const string Section = "Trading";

    public decimal StartingCash { get; set; } = 100_000m;
    public List<InstrumentOptions> Instruments { get; set; } = [];
}

public sealed class InstrumentOptions
{
    public string Symbol { get; set; } = string.Empty;
    public string BaseAsset { get; set; } = string.Empty;
    public decimal QuantityStep { get; set; }
    public decimal TickSize { get; set; }

    /// <summary>Starting price for the mock feed.</summary>
    public decimal MockStartPrice { get; set; }

    public Instrument ToInstrument() => new(Symbol, BaseAsset, QuantityStep, TickSize);
}

public enum PriceFeedMode
{
    /// <summary>Binance, falling back to the mock feed if Binance cannot be reached.</summary>
    Auto,
    Binance,
    Mock,
    /// <summary>No feed; used by tests that push prices themselves.</summary>
    None,
}

public sealed class PriceFeedOptions
{
    public const string Section = "PriceFeed";

    public PriceFeedMode Mode { get; set; } = PriceFeedMode.Auto;
    public Uri BinanceUrl { get; set; } = new("wss://data-stream.binance.vision/stream");
    public TimeSpan MockInterval { get; set; } = TimeSpan.FromSeconds(1);
}
