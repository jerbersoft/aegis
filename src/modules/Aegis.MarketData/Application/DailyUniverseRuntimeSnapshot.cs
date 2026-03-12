using Aegis.Shared.Contracts.MarketData;
using NodaTime;

namespace Aegis.MarketData.Application;

public sealed record DailyUniverseRuntimeSnapshot(
    string ProfileKey,
    Instant AsOfUtc,
    string ReadinessState,
    string ReasonCode,
    IReadOnlyList<DailySymbolRuntimeSnapshot> Symbols)
{
    public int TotalSymbolCount => Symbols.Count;

    public int ReadySymbolCount => Symbols.Count(x => x.ReadinessState == "ready");

    public int NotReadySymbolCount => Symbols.Count(x => x.ReadinessState == "not_ready");

    public DailyUniverseReadinessView ToView() =>
        new(
            ProfileKey,
            AsOfUtc,
            ReadinessState,
            ReasonCode,
            TotalSymbolCount,
            ReadySymbolCount,
            NotReadySymbolCount,
            Symbols.Select(x => x.ToView(AsOfUtc)).ToArray());

    public static DailyUniverseRuntimeSnapshot Empty(Instant asOfUtc, string profileKey = "daily_core") =>
        new(profileKey, asOfUtc, "not_requested", "none", []);
}
