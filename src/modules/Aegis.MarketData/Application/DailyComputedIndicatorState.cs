namespace Aegis.MarketData.Application;

public sealed record DailyComputedIndicatorState(
    decimal? Sma200,
    decimal? Sma50,
    decimal? Sma21,
    decimal? Sma10,
    decimal? Sma5High,
    decimal? Sma5Low,
    decimal? Sma50Volume,
    decimal? Sma21Volume,
    decimal? RelVolume21,
    decimal? RelVolume50,
    decimal? DcrPercent,
    decimal? Atr14Value,
    decimal? Atr14Percent,
    decimal? Adr14Value,
    decimal? Adr14Percent,
    decimal? Rs50,
    bool? PocketPivot,
    bool HasRequiredIndicatorState);
