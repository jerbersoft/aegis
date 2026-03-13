using Aegis.Shared.Contracts.MarketData;
using NodaTime;

namespace Aegis.MarketData.Application;

public sealed record IntradayUniverseRuntimeSnapshot(
    string Interval,
    string ProfileKey,
    Instant AsOfUtc,
    string ReadinessState,
    string ReasonCode,
    IReadOnlyList<IntradaySymbolRuntimeSnapshot> Symbols)
{
    public int TotalSymbolCount => Symbols.Count;

    public int ReadySymbolCount => Symbols.Count(x => x.ReadinessState == "ready");

    public int NotReadySymbolCount => Symbols.Count(x => x.ReadinessState != "ready");

    public int ActiveRepairSymbolCount => Symbols.Count(x => x.ActiveRepair is not null);

    public int PendingRecomputeSymbolCount => Symbols.Count(x => x.ActiveRepair?.PendingRecompute == true);

    public Instant? EarliestAffectedBarUtc => Symbols
        .Select(x => x.ActiveRepair is null ? (Instant?)null : x.ActiveRepair.EarliestAffectedBarUtc)
        .Where(x => x.HasValue)
        .DefaultIfEmpty(null)
        .Min();

    public IntradayUniverseReadinessView ToView() =>
        new(
            Interval,
            ProfileKey,
            AsOfUtc,
            ReadinessState,
            ReasonCode,
            TotalSymbolCount,
            ReadySymbolCount,
            NotReadySymbolCount,
            ActiveRepairSymbolCount,
            PendingRecomputeSymbolCount,
            EarliestAffectedBarUtc,
            Symbols.Select(x => x.ToView(AsOfUtc)).ToArray());

    public static IntradayUniverseRuntimeSnapshot Empty(Instant asOfUtc, string interval = "1min", string profileKey = "intraday_core") =>
        new(interval, profileKey, asOfUtc, "not_requested", "none", []);
}
