using Aegis.Shared.Contracts.MarketData;
using NodaTime;

namespace Aegis.MarketData.Application;

public sealed record DailySymbolRuntimeSnapshot(
    string Symbol,
    string ProfileKey,
    int RequiredBarCount,
    int AvailableBarCount,
    Instant? LastFinalizedBarUtc,
    string ReadinessState,
    string ReasonCode,
    Instant LastStateChangedUtc,
    IReadOnlyList<DailyBarView> Bars)
{
    public DailySymbolReadinessView ToView(Instant asOfUtc) =>
        new(
            Symbol,
            ProfileKey,
            asOfUtc,
            ReadinessState,
            ReasonCode,
            AvailableBarCount >= RequiredBarCount,
            RequiredBarCount,
            AvailableBarCount,
            LastFinalizedBarUtc,
            LastStateChangedUtc);
}
