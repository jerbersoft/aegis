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
    IReadOnlyList<DailyBarView> Bars,
    string? ActiveGapType,
    Instant? ActiveGapStartUtc)
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
            IndicatorState.VolumeBuzzPercent,
            IndicatorState.VolumeBuzzReferenceState.HasRequiredReferenceHistory,
            IndicatorState.VolumeBuzzReferenceState.RequiredReferenceSessionCount,
            IndicatorState.VolumeBuzzReferenceState.AvailableReferenceSessionCount,
            RequiredBarCount,
            AvailableBarCount,
            LastFinalizedBarUtc,
            LastStateChangedUtc,
            ActiveGapType,
            ActiveGapStartUtc);
}
