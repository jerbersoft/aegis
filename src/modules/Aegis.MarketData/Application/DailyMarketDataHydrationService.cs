using Aegis.MarketData.Application.Abstractions;
using Aegis.MarketData.Infrastructure;
using Aegis.Shared.Contracts.MarketData;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Aegis.MarketData.Application;

public sealed class DailyMarketDataHydrationService(
    MarketDataDbContext dbContext,
    IMarketDataSymbolDemandReader demandReader,
    MarketDataDailyRuntimeStore runtimeStore,
    IClock clock)
{
    public const string DailyCoreProfileKey = "daily_core";
    public const int DailyCoreRequiredBarCount = 200;
    public const int DailyRuntimeRetentionCount = 300;

    public async Task<DailyUniverseRuntimeSnapshot> RebuildAsync(string? overrideReadinessState = null, string? overrideReasonCode = null, CancellationToken cancellationToken = default)
    {
        // Hydration always rebuilds from persisted bars so readiness stays DB-derived rather than provider-derived.
        var asOfUtc = clock.GetCurrentInstant();
        var baseDemand = await demandReader.GetDailyDemandAsync(cancellationToken);
        var demand = DailyMarketDataDemandExpander.Expand(baseDemand);
        var demandBySymbol = demand.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);

        var symbols = demand
            .Where(x => x.ProfileKeys.Contains(DailyCoreProfileKey, StringComparer.OrdinalIgnoreCase))
            .Select(x => x.Symbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (symbols.Length == 0)
        {
            var empty = new DailyUniverseRuntimeSnapshot(DailyCoreProfileKey, asOfUtc, overrideReadinessState ?? "not_requested", overrideReasonCode ?? "none", []);
            runtimeStore.SetSnapshot(empty);
            return empty;
        }

        var rows = await dbContext.Bars
            .AsNoTracking()
            .Where(x => x.Interval == "1day" && symbols.Contains(x.Symbol))
            .OrderBy(x => x.Symbol)
            .ThenByDescending(x => x.BarTimeUtc)
            .Select(x => new DailyBarView(
                x.Symbol,
                x.Interval,
                x.BarTimeUtc,
                x.Open,
                x.High,
                x.Low,
                x.Close,
                x.Volume,
                x.SessionType,
                x.MarketDate,
                x.ProviderName,
                x.ProviderFeed,
                x.RuntimeState,
                x.IsReconciled))
            .ToListAsync(cancellationToken);

        var grouped = rows
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<DailyBarView>)x.Take(DailyRuntimeRetentionCount).OrderBy(y => y.BarTimeUtc).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var preliminarySnapshots = symbols.ToDictionary(
            symbol => symbol,
            symbol => BuildPreliminarySnapshot(symbol, demandBySymbol[symbol], grouped.TryGetValue(symbol, out var bars) ? bars : [], asOfUtc),
            StringComparer.OrdinalIgnoreCase);

        // Final readiness is resolved in a second pass so benchmark-dependent symbols can inspect benchmark state.
        var finalSnapshots = symbols
            .Select(symbol => BuildFinalSnapshot(preliminarySnapshots[symbol], preliminarySnapshots, asOfUtc))
            .ToArray();

        var rollupReadiness = overrideReadinessState
            ?? (finalSnapshots.Any(x => x.ReadinessState == "not_ready") ? "not_ready" : "ready");

        var rollupReason = overrideReasonCode
            ?? (finalSnapshots.FirstOrDefault(x => x.ReadinessState == "not_ready")?.ReasonCode ?? "none");

        var snapshot = new DailyUniverseRuntimeSnapshot(DailyCoreProfileKey, asOfUtc, rollupReadiness, rollupReason, finalSnapshots);
        runtimeStore.SetSnapshot(snapshot);
        return snapshot;
    }

    private static DailySymbolRuntimeSnapshot BuildPreliminarySnapshot(string symbol, DailySymbolDemand demand, IReadOnlyList<DailyBarView> bars, Instant asOfUtc)
    {
        var availableBarCount = bars.Count;
        var hasRequiredBars = availableBarCount >= DailyCoreRequiredBarCount;
        var isBenchmark = string.Equals(demand.DemandTier, DailyMarketDataDemandExpander.BenchmarkDemandTier, StringComparison.OrdinalIgnoreCase);
        var hasBenchmarkDependency = !isBenchmark && !string.Equals(symbol, DailyMarketDataDemandExpander.BenchmarkSymbol, StringComparison.OrdinalIgnoreCase);
        var lastFinalizedBarUtc = bars.LastOrDefault()?.BarTimeUtc;

        return new DailySymbolRuntimeSnapshot(
            symbol,
            DailyCoreProfileKey,
            DailyCoreRequiredBarCount,
            availableBarCount,
            lastFinalizedBarUtc,
            hasRequiredBars ? "ready" : "not_ready",
            hasRequiredBars ? "none" : "missing_required_bars",
            asOfUtc,
            hasBenchmarkDependency,
            hasBenchmarkDependency ? DailyMarketDataDemandExpander.BenchmarkSymbol : null,
            null,
            BuildIndicatorState(bars, null, hasBenchmarkDependency),
            bars);
    }

    private static DailySymbolRuntimeSnapshot BuildFinalSnapshot(
        DailySymbolRuntimeSnapshot snapshot,
        IReadOnlyDictionary<string, DailySymbolRuntimeSnapshot> preliminarySnapshots,
        Instant asOfUtc)
    {
        if (!snapshot.HasBenchmarkDependency)
        {
            var indicatorState = BuildIndicatorState(snapshot.Bars, null, false);
            return ApplyIndicatorState(snapshot with { BenchmarkReadinessState = snapshot.BenchmarkSymbol is null ? null : "ready" }, indicatorState, asOfUtc);
        }

        if (snapshot.BenchmarkSymbol is null || !preliminarySnapshots.TryGetValue(snapshot.BenchmarkSymbol, out var benchmarkSnapshot))
        {
            return snapshot with
            {
                ReadinessState = "not_ready",
                ReasonCode = "gap_benchmark_dependency",
                BenchmarkReadinessState = null,
                LastStateChangedUtc = asOfUtc,
                IndicatorState = BuildIndicatorState(snapshot.Bars, null, snapshot.HasBenchmarkDependency)
            };
        }

        if (snapshot.ReadinessState != "ready")
        {
            return snapshot with
            {
                BenchmarkReadinessState = benchmarkSnapshot.ReadinessState,
                IndicatorState = BuildIndicatorState(snapshot.Bars, benchmarkSnapshot.Bars, snapshot.HasBenchmarkDependency)
            };
        }

        if (benchmarkSnapshot.ReadinessState != "ready")
        {
            return snapshot with
            {
                ReadinessState = "not_ready",
                ReasonCode = "benchmark_not_ready",
                BenchmarkReadinessState = benchmarkSnapshot.ReadinessState,
                LastStateChangedUtc = asOfUtc,
                IndicatorState = BuildIndicatorState(snapshot.Bars, benchmarkSnapshot.Bars, snapshot.HasBenchmarkDependency)
            };
        }

        var finalIndicatorState = BuildIndicatorState(snapshot.Bars, benchmarkSnapshot.Bars, snapshot.HasBenchmarkDependency);
        return ApplyIndicatorState(snapshot with { BenchmarkReadinessState = benchmarkSnapshot.ReadinessState }, finalIndicatorState, asOfUtc);
    }

    private static DailySymbolRuntimeSnapshot ApplyIndicatorState(DailySymbolRuntimeSnapshot snapshot, DailyComputedIndicatorState indicatorState, Instant asOfUtc)
    {
        if (snapshot.ReadinessState != "ready")
        {
            return snapshot with { IndicatorState = indicatorState };
        }

        // Once bars and benchmark state are good enough, indicator availability becomes the final readiness gate.
        if (!indicatorState.HasRequiredIndicatorState)
        {
            return snapshot with
            {
                ReadinessState = "not_ready",
                ReasonCode = "awaiting_recompute",
                LastStateChangedUtc = asOfUtc,
                IndicatorState = indicatorState
            };
        }

        return snapshot with { IndicatorState = indicatorState };
    }

    private static DailyComputedIndicatorState BuildIndicatorState(IReadOnlyList<DailyBarView> symbolBars, IReadOnlyList<DailyBarView>? benchmarkBars, bool requiresBenchmark)
    {
        var sma200 = CalculateSma(symbolBars, 200, bar => bar.Close);
        var sma50 = CalculateSma(symbolBars, 50, bar => bar.Close);
        var sma21 = CalculateSma(symbolBars, 21, bar => bar.Close);
        var sma10 = CalculateSma(symbolBars, 10, bar => bar.Close);
        var sma5High = CalculateSma(symbolBars, 5, bar => bar.High);
        var sma5Low = CalculateSma(symbolBars, 5, bar => bar.Low);
        var sma50Volume = CalculateSma(symbolBars, 50, bar => bar.Volume);
        var sma21Volume = CalculateSma(symbolBars, 21, bar => bar.Volume);
        var relVolume21 = CalculateRelativeVolume(symbolBars, sma21Volume);
        var relVolume50 = CalculateRelativeVolume(symbolBars, sma50Volume);
        var dcrPercent = CalculateDcrPercent(symbolBars);
        var atr14Value = CalculateAtr14Value(symbolBars);
        var atr14Percent = CalculateAtr14Percent(symbolBars);
        var adr14Value = CalculateAdr14Value(symbolBars);
        var adr14Percent = CalculateAdr14Percent(symbolBars, adr14Value);
        // rs_50 is the first benchmark-relative signal in the runtime, so it is only required for benchmark-aware symbols.
        var rs50 = requiresBenchmark ? CalculateRs50(symbolBars, benchmarkBars, atr14Percent) : null;
        var pocketPivot = CalculatePocketPivot(symbolBars, dcrPercent);
        var hasRequiredIndicatorState = sma200.HasValue
                                        && sma50.HasValue
                                        && sma21.HasValue
                                        && sma10.HasValue
                                        && dcrPercent.HasValue
                                        && atr14Value.HasValue
                                        && atr14Percent.HasValue
                                        && adr14Value.HasValue
                                        && adr14Percent.HasValue
                                        && (!requiresBenchmark || rs50.HasValue);

        return new DailyComputedIndicatorState(
            sma200,
            sma50,
            sma21,
            sma10,
            sma5High,
            sma5Low,
            sma50Volume,
            sma21Volume,
            relVolume21,
            relVolume50,
            dcrPercent,
            atr14Value,
            atr14Percent,
            adr14Value,
            adr14Percent,
            rs50,
            pocketPivot,
            hasRequiredIndicatorState);
    }

    private static decimal? CalculateSma(IReadOnlyList<DailyBarView> bars, int period, Func<DailyBarView, decimal> selector)
    {
        if (bars.Count < period)
        {
            return null;
        }

        return bars.TakeLast(period).Average(selector);
    }

    private static decimal? CalculateRelativeVolume(IReadOnlyList<DailyBarView> bars, decimal? averageVolume)
    {
        if (!averageVolume.HasValue || averageVolume.Value == 0 || bars.Count == 0)
        {
            return null;
        }

        return bars[^1].Volume / averageVolume.Value;
    }

    private static decimal? CalculateDcrPercent(IReadOnlyList<DailyBarView> bars)
    {
        if (bars.Count == 0)
        {
            return null;
        }

        var current = bars[^1];
        var range = current.High - current.Low;
        if (range == 0)
        {
            return null;
        }

        return ((current.Close - current.Low) / range) * 100m;
    }

    private static decimal? CalculateAtr14Value(IReadOnlyList<DailyBarView> bars)
    {
        if (bars.Count < 15)
        {
            return null;
        }

        var workingBars = bars.TakeLast(15).ToArray();
        var trueRanges = new decimal[14];

        for (var index = 1; index < workingBars.Length; index++)
        {
            var current = workingBars[index];
            var prior = workingBars[index - 1];
            var highLow = current.High - current.Low;
            var highPriorClose = Math.Abs(current.High - prior.Close);
            var lowPriorClose = Math.Abs(current.Low - prior.Close);
            trueRanges[index - 1] = Math.Max(highLow, Math.Max(highPriorClose, lowPriorClose));
        }

        return trueRanges.Average();
    }

    private static decimal? CalculateAtr14Percent(IReadOnlyList<DailyBarView> bars)
    {
        var atr14Value = CalculateAtr14Value(bars);
        if (!atr14Value.HasValue || bars.Count == 0 || bars[^1].Close == 0)
        {
            return null;
        }

        return (atr14Value.Value / bars[^1].Close) * 100m;
    }

    private static decimal? CalculateAdr14Value(IReadOnlyList<DailyBarView> bars)
    {
        if (bars.Count < 14)
        {
            return null;
        }

        return bars.TakeLast(14).Average(bar => bar.High - bar.Low);
    }

    private static decimal? CalculateAdr14Percent(IReadOnlyList<DailyBarView> bars, decimal? adr14Value)
    {
        if (!adr14Value.HasValue || bars.Count == 0 || bars[^1].Close == 0)
        {
            return null;
        }

        return (adr14Value.Value / bars[^1].Close) * 100m;
    }

    private static decimal? CalculateRs50(IReadOnlyList<DailyBarView> symbolBars, IReadOnlyList<DailyBarView>? benchmarkBars, decimal? atr14Percent)
    {
        if (!atr14Percent.HasValue || atr14Percent.Value == 0 || symbolBars.Count < 51 || benchmarkBars is null || benchmarkBars.Count < 51)
        {
            return null;
        }

        var symbolWindow = symbolBars.TakeLast(51).ToArray();
        var benchmarkWindow = benchmarkBars.TakeLast(51).ToArray();
        var currentClose = symbolWindow[^1].Close;
        var priorClose = symbolWindow[0].Close;
        var benchmarkClose = benchmarkWindow[^1].Close;
        var benchmarkPriorClose = benchmarkWindow[0].Close;

        if (priorClose == 0 || benchmarkPriorClose == 0)
        {
            return null;
        }

        var symbolReturnPct = ((currentClose / priorClose) - 1m) * 100m;
        var benchmarkReturnPct = ((benchmarkClose / benchmarkPriorClose) - 1m) * 100m;
        var relativeReturnPct = symbolReturnPct - benchmarkReturnPct;
        return relativeReturnPct / atr14Percent.Value;
    }

    private static bool? CalculatePocketPivot(IReadOnlyList<DailyBarView> bars, decimal? dcrPercent)
    {
        if (bars.Count < 12 || !dcrPercent.HasValue || dcrPercent.Value <= 50m)
        {
            return null;
        }

        var current = bars[^1];
        var priorBars = bars.TakeLast(11).Take(10).ToArray();
        var priorRedVolumes = new List<long>();

        for (var index = 1; index < priorBars.Length; index++)
        {
            if (priorBars[index].Close < priorBars[index - 1].Close)
            {
                priorRedVolumes.Add(priorBars[index].Volume);
            }
        }

        if (priorRedVolumes.Count == 0)
        {
            return false;
        }

        return current.Volume > priorRedVolumes.Max();
    }
}
