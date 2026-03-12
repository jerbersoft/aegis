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

    public int NotReadySymbolCount => Symbols.Count(x => x.ReadinessState == "not_ready");

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
            Symbols.Select(x => x.ToView(AsOfUtc)).ToArray());

    public static IntradayUniverseRuntimeSnapshot Empty(Instant asOfUtc, string interval = "1min", string profileKey = "intraday_core") =>
        new(interval, profileKey, asOfUtc, "not_requested", "none", []);
}
