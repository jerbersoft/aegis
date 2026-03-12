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
            symbol => BuildPreliminarySnapshot(
                symbol,
                demandBySymbol[symbol],
                grouped.TryGetValue(symbol, out var bars) ? bars : [],
                asOfUtc),
            StringComparer.OrdinalIgnoreCase);

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
            bars);
    }

    private static DailySymbolRuntimeSnapshot BuildFinalSnapshot(
        DailySymbolRuntimeSnapshot snapshot,
        IReadOnlyDictionary<string, DailySymbolRuntimeSnapshot> preliminarySnapshots,
        Instant asOfUtc)
    {
        if (!snapshot.HasBenchmarkDependency)
        {
            return snapshot with { BenchmarkReadinessState = snapshot.BenchmarkSymbol is null ? null : "ready" };
        }

        if (snapshot.BenchmarkSymbol is null || !preliminarySnapshots.TryGetValue(snapshot.BenchmarkSymbol, out var benchmarkSnapshot))
        {
            return snapshot with
            {
                ReadinessState = "not_ready",
                ReasonCode = "gap_benchmark_dependency",
                BenchmarkReadinessState = null,
                LastStateChangedUtc = asOfUtc
            };
        }

        if (snapshot.ReadinessState != "ready")
        {
            return snapshot with { BenchmarkReadinessState = benchmarkSnapshot.ReadinessState };
        }

        if (benchmarkSnapshot.ReadinessState != "ready")
        {
            return snapshot with
            {
                ReadinessState = "not_ready",
                ReasonCode = "benchmark_not_ready",
                BenchmarkReadinessState = benchmarkSnapshot.ReadinessState,
                LastStateChangedUtc = asOfUtc
            };
        }

        return snapshot with { BenchmarkReadinessState = benchmarkSnapshot.ReadinessState };
    }
}
