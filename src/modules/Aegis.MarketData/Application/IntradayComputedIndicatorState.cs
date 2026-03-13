namespace Aegis.MarketData.Application;

public sealed record IntradayComputedIndicatorState(
    decimal? Ema30,
    decimal? Ema100,
    decimal? VolumeBuzzPercent,
    decimal? Vwap,
    bool HasRequiredIndicatorState,
    IntradayVolumeBuzzReferenceState VolumeBuzzReferenceState);
