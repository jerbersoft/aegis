using Aegis.MarketData.Application.Abstractions;
using Aegis.MarketData.Domain.Entities;
using Aegis.MarketData.Infrastructure;
using Aegis.Shared.Contracts.MarketData;
using Aegis.Shared.Ports.MarketData;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Aegis.MarketData.Application;

public sealed class MarketDataBootstrapService(
    MarketDataDbContext dbContext,
    IMarketDataSymbolDemandReader demandReader,
    IHistoricalBarProvider historicalBarProvider,
    MarketDataBootstrapStateStore stateStore,
    DailyMarketDataHydrationService hydrationService,
    MarketDataDailyRuntimeStore runtimeStore,
    IClock clock)
{
    private const string DailyInterval = "1day";
    private const string HistoricalFeed = "iex";
    private const int MissingBarSafetyBuffer = 60;
    private static readonly Duration DefaultLookbackWindow = Duration.FromDays(450);

    public async Task<MarketDataBootstrapStatusView> RunWarmupAsync(CancellationToken cancellationToken)
    {
        // Expand raw watchlist demand into the full daily warmup set, including benchmark dependencies.
        var demand = DailyMarketDataDemandExpander.Expand(await demandReader.GetDailyDemandAsync(cancellationToken));
        var symbols = demand
            .Select(x => x.Symbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var startedAt = clock.GetCurrentInstant();

        if (symbols.Length == 0)
        {
            var emptySnapshot = await hydrationService.RebuildAsync("not_requested", "none", cancellationToken);
            var emptyStatus = await BuildStatusAsync(emptySnapshot, [], symbols, null, cancellationToken);
            stateStore.SetStatus(emptyStatus);
            return emptyStatus;
        }

        runtimeStore.SetSnapshot(new DailyUniverseRuntimeSnapshot(
            DailyMarketDataHydrationService.DailyCoreProfileKey,
            startedAt,
            "warming_up",
            "warmup_in_progress",
            runtimeStore.GetSnapshot().Symbols));

        var failedSymbols = new List<string>();

        foreach (var symbol in symbols)
        {
            var coverage = await GetDailyCoverageAsync(symbol, cancellationToken);
            if (!coverage.NeedsBackfill)
            {
                continue;
            }

            // When we already have partial history, request older bars instead of re-fetching the same recent window.
            var request = BuildHistoricalRequest(coverage, startedAt);
            var batch = await historicalBarProvider.GetDailyBarsAsync(request, cancellationToken);
            if (!batch.Succeeded)
            {
                failedSymbols.Add(symbol);
                continue;
            }

            await UpsertBarsAsync(batch, startedAt, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var snapshot = await hydrationService.RebuildAsync(cancellationToken: cancellationToken);
        if (failedSymbols.Count > 0 && snapshot.ReadinessState == "ready")
        {
            snapshot = snapshot with
            {
                ReadinessState = "not_ready",
                ReasonCode = "historical_data_unavailable"
            };
            runtimeStore.SetSnapshot(snapshot);
        }

        var status = await BuildStatusAsync(snapshot, failedSymbols, symbols, startedAt, cancellationToken);
        stateStore.SetStatus(status);
        return status;
    }

    public async Task<MarketDataBootstrapStatusView> GetStatusAsync(CancellationToken cancellationToken)
    {
        var current = stateStore.GetStatus();
        var snapshot = await hydrationService.RebuildAsync(cancellationToken: cancellationToken);
        var status = await BuildStatusAsync(snapshot, current.FailedSymbols, snapshot.Symbols.Select(x => x.Symbol).ToArray(), current.LastWarmupUtc, cancellationToken);
        stateStore.SetStatus(status);
        return status;
    }

    public Task<DailyUniverseReadinessView> GetDailyReadinessAsync(CancellationToken cancellationToken) =>
        Task.FromResult(runtimeStore.GetSnapshot().ToView());

    public Task<DailySymbolReadinessView?> GetDailyReadinessAsync(string symbol, CancellationToken cancellationToken)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return Task.FromResult<DailySymbolReadinessView?>(null);
        }

        var snapshot = runtimeStore.GetSnapshot();
        return Task.FromResult(snapshot.Symbols.FirstOrDefault(x => x.Symbol == normalizedSymbol)?.ToView(snapshot.AsOfUtc));
    }

    public async Task<DailyBarsView?> GetDailyBarsAsync(string symbol, CancellationToken cancellationToken)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return null;
        }

        var items = await dbContext.Bars
            .AsNoTracking()
            .Where(x => x.Symbol == normalizedSymbol && x.Interval == DailyInterval)
            .OrderByDescending(x => x.BarTimeUtc)
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

        return items.Count == 0 ? null : new DailyBarsView(normalizedSymbol, items.Count, items);
    }

    private async Task<DailyHistoryCoverage> GetDailyCoverageAsync(string symbol, CancellationToken cancellationToken)
    {
        // Coverage is used only to decide whether warmup needs more history and which direction to fetch.
        var items = await dbContext.Bars
            .AsNoTracking()
            .Where(x => x.Symbol == symbol && x.Interval == DailyInterval)
            .OrderBy(x => x.BarTimeUtc)
            .Select(x => x.BarTimeUtc)
            .ToListAsync(cancellationToken);

        var persistedCount = items.Count;
        var earliestBarUtc = items.FirstOrDefault();
        var latestBarUtc = items.LastOrDefault();
        var missingBarCount = Math.Max(0, DailyMarketDataHydrationService.DailyCoreRequiredBarCount - persistedCount);

        return new DailyHistoryCoverage(symbol, persistedCount, earliestBarUtc == default ? null : earliestBarUtc, latestBarUtc == default ? null : latestBarUtc, missingBarCount);
    }

    private static HistoricalBarRequest BuildHistoricalRequest(DailyHistoryCoverage coverage, Instant now)
    {
        var limit = Math.Max(DailyMarketDataHydrationService.DailyCoreRequiredBarCount + MissingBarSafetyBuffer, coverage.MissingBarCount + MissingBarSafetyBuffer);

        if (coverage.EarliestBarUtc is { } earliestBarUtc)
        {
            // Backfill from the earliest persisted bar so missing-history fixes preserve the newest data already on disk.
            var toUtc = earliestBarUtc;
            var fromUtc = toUtc - Duration.FromDays(limit * 2);
            return new HistoricalBarRequest(coverage.Symbol, fromUtc, toUtc, limit, HistoricalFeed);
        }

        return new HistoricalBarRequest(coverage.Symbol, now - DefaultLookbackWindow, now, limit, HistoricalFeed);
    }

    private async Task UpsertBarsAsync(HistoricalBarBatchResult batch, Instant now, CancellationToken cancellationToken)
    {
        foreach (var bar in batch.Bars)
        {
            var existing = await dbContext.Bars.SingleOrDefaultAsync(
                x => x.Symbol == bar.Symbol && x.Interval == bar.Interval && x.BarTimeUtc == bar.BarTimeUtc,
                cancellationToken);

            if (existing is null)
            {
                dbContext.Bars.Add(new MarketDataBar
                {
                    BarId = Guid.NewGuid(),
                    Symbol = bar.Symbol,
                    Interval = bar.Interval,
                    BarTimeUtc = bar.BarTimeUtc,
                    Open = bar.Open,
                    High = bar.High,
                    Low = bar.Low,
                    Close = bar.Close,
                    Volume = bar.Volume,
                    SessionType = bar.SessionType,
                    MarketDate = bar.MarketDate,
                    ProviderName = batch.ProviderName,
                    ProviderFeed = batch.ProviderFeed ?? string.Empty,
                    RuntimeState = bar.RuntimeState,
                    IsReconciled = bar.IsReconciled,
                    CreatedUtc = now,
                    UpdatedUtc = now
                });
            }
            else
            {
                existing.Open = bar.Open;
                existing.High = bar.High;
                existing.Low = bar.Low;
                existing.Close = bar.Close;
                existing.Volume = bar.Volume;
                existing.SessionType = bar.SessionType;
                existing.MarketDate = bar.MarketDate;
                existing.ProviderName = batch.ProviderName;
                existing.ProviderFeed = batch.ProviderFeed ?? string.Empty;
                existing.RuntimeState = bar.RuntimeState;
                existing.IsReconciled = bar.IsReconciled;
                existing.UpdatedUtc = now;
            }
        }
    }

    private async Task<MarketDataBootstrapStatusView> BuildStatusAsync(
        DailyUniverseRuntimeSnapshot snapshot,
        IReadOnlyCollection<string> failedSymbols,
        IReadOnlyList<string> demandSymbols,
        Instant? lastWarmupUtc,
        CancellationToken cancellationToken)
    {
        var persistedBarCount = await dbContext.Bars.CountAsync(cancellationToken);
        var warmedSymbolCount = snapshot.Symbols.Count(x => x.AvailableBarCount > 0);
        var reasonCode = failedSymbols.Count > 0 && snapshot.ReadinessState != "ready"
            ? snapshot.ReasonCode == "none" ? "historical_data_unavailable" : snapshot.ReasonCode
            : snapshot.ReasonCode;

        return new MarketDataBootstrapStatusView(
            snapshot.ReadinessState,
            reasonCode,
            snapshot.ProfileKey,
            demandSymbols.Count,
            warmedSymbolCount,
            snapshot.ReadySymbolCount,
            snapshot.NotReadySymbolCount,
            persistedBarCount,
            snapshot.AsOfUtc,
            lastWarmupUtc,
            demandSymbols,
            failedSymbols.Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private sealed record DailyHistoryCoverage(
        string Symbol,
        int PersistedCount,
        Instant? EarliestBarUtc,
        Instant? LatestBarUtc,
        int MissingBarCount)
    {
        public bool NeedsBackfill => PersistedCount < DailyMarketDataHydrationService.DailyCoreRequiredBarCount;
    }
}
