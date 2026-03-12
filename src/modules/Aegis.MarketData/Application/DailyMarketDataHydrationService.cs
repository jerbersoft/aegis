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
        var demand = await demandReader.GetDailyDemandAsync(cancellationToken);
        var symbols = demand
            .Where(x => x.ProfileKeys.Contains(DailyCoreProfileKey, StringComparer.OrdinalIgnoreCase))
            .Select(x => x.Symbol.Trim().ToUpperInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
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

        var symbolSnapshots = symbols.Select(symbol => BuildSymbolSnapshot(symbol, grouped.TryGetValue(symbol, out var bars) ? bars : [], asOfUtc)).ToArray();

        var rollupReadiness = overrideReadinessState
            ?? (symbolSnapshots.Any(x => x.ReadinessState == "not_ready") ? "not_ready" : "ready");

        var rollupReason = overrideReasonCode
            ?? (symbolSnapshots.FirstOrDefault(x => x.ReadinessState == "not_ready")?.ReasonCode ?? "none");

        var snapshot = new DailyUniverseRuntimeSnapshot(DailyCoreProfileKey, asOfUtc, rollupReadiness, rollupReason, symbolSnapshots);
        runtimeStore.SetSnapshot(snapshot);
        return snapshot;
    }

    private static DailySymbolRuntimeSnapshot BuildSymbolSnapshot(string symbol, IReadOnlyList<DailyBarView> bars, Instant asOfUtc)
    {
        var availableBarCount = bars.Count;
        var isReady = availableBarCount >= DailyCoreRequiredBarCount;
        var readinessState = isReady ? "ready" : "not_ready";
        var reasonCode = isReady ? "none" : "missing_required_bars";
        var lastFinalizedBarUtc = bars.LastOrDefault()?.BarTimeUtc;

        return new DailySymbolRuntimeSnapshot(
            symbol,
            DailyCoreProfileKey,
            DailyCoreRequiredBarCount,
            availableBarCount,
            lastFinalizedBarUtc,
            readinessState,
            reasonCode,
            asOfUtc,
            bars);
    }
}
