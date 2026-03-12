namespace Aegis.MarketData.Application;

public sealed record DailyComputedIndicatorState(
    decimal? Sma200,
    decimal? Atr14Percent,
    decimal? Rs50,
    bool HasRequiredIndicatorState);
