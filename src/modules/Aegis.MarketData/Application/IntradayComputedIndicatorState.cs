namespace Aegis.MarketData.Application;

public sealed record IntradayComputedIndicatorState(
    decimal? Ema30,
    decimal? Ema100,
    decimal? Vwap,
    bool HasRequiredIndicatorState);
