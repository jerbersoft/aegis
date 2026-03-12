using Aegis.Shared.Contracts.MarketData;
using NodaTime;

namespace Aegis.MarketData.Application;

public sealed record IntradaySymbolRuntimeSnapshot(
    string Symbol,
    string Interval,
    string ProfileKey,
    int RequiredBarCount,
    int AvailableBarCount,
    Instant? LastFinalizedBarUtc,
    string ReadinessState,
    string ReasonCode,
    Instant LastStateChangedUtc,
    IntradayComputedIndicatorState IndicatorState,
    IReadOnlyList<DailyBarView> Bars)
{
    public IntradaySymbolReadinessView ToView(Instant asOfUtc) =>
        new(
            Symbol,
            Interval,
            ProfileKey,
            asOfUtc,
            ReadinessState,
            ReasonCode,
            AvailableBarCount >= RequiredBarCount,
            IndicatorState.HasRequiredIndicatorState,
            RequiredBarCount,
            AvailableBarCount,
            LastFinalizedBarUtc,
            LastStateChangedUtc);
}
