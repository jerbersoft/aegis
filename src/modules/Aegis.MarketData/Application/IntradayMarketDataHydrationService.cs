using Aegis.MarketData.Application.Abstractions;
using Aegis.MarketData.Domain.Entities;
using Aegis.MarketData.Infrastructure;
using Aegis.Shared.Contracts.MarketData;
using Aegis.Shared.Ports.MarketData;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Aegis.MarketData.Application;

public sealed class IntradayMarketDataHydrationService(
    MarketDataDbContext dbContext,
    IMarketDataSymbolDemandReader demandReader,
    MarketDataIntradayRuntimeStore runtimeStore,
    IHistoricalBarProvider historicalBarProvider,
    IClock clock)
{
    private static readonly DateTimeZone MarketTimeZone = DateTimeZoneProviders.Tzdb["America/New_York"];
    private const string HistoricalFeed = "iex";
    private const int RepairRequestSafetyBufferBars = 30;

    public const string IntradayCoreProfileKey = "intraday_core";
    public const string IntradayInterval = "1min";
    public const int IntradayRequiredBarCount = 100;
    public const int IntradaySessionBarCount = 390;
    public const int VolumeBuzzReferenceSessionCount = 10;

    public async Task<IntradayUniverseRuntimeSnapshot> RebuildAsync(string? overrideReadinessState = null, string? overrideReasonCode = null, CancellationToken cancellationToken = default)
    {
        var asOfUtc = clock.GetCurrentInstant();
        var demand = await demandReader.GetIntradayDemandAsync(cancellationToken);
        var intradayDemand = demand
            .Where(x => string.Equals(x.Interval, IntradayInterval, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (intradayDemand.Length == 0)
        {
            var empty = new IntradayUniverseRuntimeSnapshot(IntradayInterval, IntradayCoreProfileKey, asOfUtc, overrideReadinessState ?? "not_requested", overrideReasonCode ?? "none", []);
            runtimeStore.SetSnapshot(empty);
            return empty;
        }

        var symbols = intradayDemand.Select(x => x.Symbol.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var grouped = await LoadBarsBySymbolAsync(symbols, cancellationToken);
        var previousSnapshot = runtimeStore.GetSnapshot();
        var previousSymbolSnapshots = previousSnapshot.Symbols.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);
        var completedRecomputes = new Dictionary<string, SnapshotComputationOverride>(StringComparer.OrdinalIgnoreCase);
        var failedRepairs = new Dictionary<string, SnapshotComputationOverride>(StringComparer.OrdinalIgnoreCase);
        var pendingRecompute = new Dictionary<string, SnapshotComputationOverride>(StringComparer.OrdinalIgnoreCase);
        var deferredRepairs = new Dictionary<string, SnapshotComputationOverride>(StringComparer.OrdinalIgnoreCase);
        var repairCandidates = new List<RepairCandidate>();

        foreach (var symbol in symbols)
        {
            grouped.TryGetValue(symbol, out var existingBars);
            existingBars ??= [];
            previousSymbolSnapshots.TryGetValue(symbol, out var priorSymbolSnapshot);
            var repairAssessment = BuildRepairAssessment(symbol, FilterToCurrentAndPriorSession(existingBars), asOfUtc, priorSymbolSnapshot?.ActiveRepair);
            if (repairAssessment.ActiveRepair is null)
            {
                continue;
            }

            repairCandidates.Add(new RepairCandidate(symbol, existingBars, priorSymbolSnapshot, repairAssessment.ActiveRepair));
        }

        var scheduledSymbols = SelectRepairExecutionSymbols(repairCandidates, asOfUtc);
        var orderedCandidates = repairCandidates
            .OrderByDescending(candidate => scheduledSymbols.Contains(candidate.Symbol))
            .ThenByDescending(candidate => GetRepairPriorityRank(candidate.ActiveRepair.PriorityTier))
            .ThenBy(candidate => candidate.ActiveRepair.EarliestAffectedBarUtc)
            .ThenBy(candidate => candidate.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var candidate in orderedCandidates)
        {
            if (!scheduledSymbols.Contains(candidate.Symbol))
            {
                var deferredReasonCode = candidate.ActiveRepair.CanStartAttempt(asOfUtc)
                    ? candidate.ActiveRepair.PrimaryReasonCode
                    : candidate.PriorSnapshot?.ReasonCode ?? candidate.ActiveRepair.PrimaryReasonCode;
                deferredRepairs[candidate.Symbol] = new SnapshotComputationOverride(
                    candidate.ActiveRepair,
                    deferredReasonCode,
                    null,
                    null);
                continue;
            }

            var repairResult = candidate.ActiveRepair.PendingRecompute
                ? ExecutePendingRecompute(candidate.ExistingBars, candidate.ActiveRepair, candidate.PriorSnapshot, asOfUtc)
                : await ExecuteRepairAsync(
                    candidate.Symbol,
                    candidate.ExistingBars,
                    candidate.ActiveRepair,
                    asOfUtc,
                    cancellationToken);

            if (repairResult.RepairedBars is not null)
            {
                grouped[candidate.Symbol] = repairResult.RepairedBars;
                if (repairResult.ActiveRepair is null)
                {
                    completedRecomputes[candidate.Symbol] = new SnapshotComputationOverride(null, null, repairResult.RecomputedFromUtc, repairResult.RecomputedIndicatorState);
                }
                else if (repairResult.RecomputedFromUtc.HasValue)
                {
                    pendingRecompute[candidate.Symbol] = new SnapshotComputationOverride(
                        repairResult.ActiveRepair,
                        IntradayRepairState.AwaitingRecomputeReasonCode,
                        repairResult.RecomputedFromUtc,
                        null);
                }
            }

            if (repairResult.RepairedBars is null && repairResult.FailureReasonCode is not null && repairResult.ActiveRepair is not null)
            {
                failedRepairs[candidate.Symbol] = new SnapshotComputationOverride(repairResult.ActiveRepair, repairResult.FailureReasonCode, null, null);
            }
        }

        var finalOverrides = completedRecomputes
            .Concat(pendingRecompute)
            .Concat(deferredRepairs)
            .Concat(failedRepairs)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var snapshot = BuildUniverseSnapshot(symbols, grouped, asOfUtc, overrideReadinessState, overrideReasonCode, finalOverrides);

        foreach (var symbol in completedRecomputes.Keys)
        {
            var repairedSnapshot = snapshot.Symbols.First(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (ValidateRepairedSequence(repairedSnapshot.Bars, asOfUtc))
            {
                continue;
            }

            finalOverrides[symbol] = new SnapshotComputationOverride(
                repairCandidates.Single(candidate => string.Equals(candidate.Symbol, symbol, StringComparison.OrdinalIgnoreCase)).ActiveRepair.MarkFailed(asOfUtc),
                IntradayRepairState.RepairValidationFailedReasonCode,
                completedRecomputes[symbol].RecomputedFromUtc,
                null);
        }

        snapshot = BuildUniverseSnapshot(symbols, grouped, asOfUtc, overrideReadinessState, overrideReasonCode, finalOverrides);
        runtimeStore.SetSnapshot(snapshot);
        return snapshot;
    }

    private async Task<Dictionary<string, IReadOnlyList<DailyBarView>>> LoadBarsBySymbolAsync(IReadOnlyList<string> symbols, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Bars
            .AsNoTracking()
            .Where(x => x.Interval == IntradayInterval && symbols.Contains(x.Symbol))
            .OrderBy(x => x.Symbol)
            .ThenBy(x => x.BarTimeUtc)
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

        return rows
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<DailyBarView>)x.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IntradayRepairExecutionResult> ExecuteRepairAsync(
        string symbol,
        IReadOnlyList<DailyBarView> existingBars,
        IntradayRepairState repairState,
        Instant asOfUtc,
        CancellationToken cancellationToken)
    {
        var activeAttempt = repairState.MarkAttemptStarted(asOfUtc);
        var request = BuildRepairRequest(activeAttempt, asOfUtc);
        var batch = await historicalBarProvider.GetIntradayBarsAsync(request, cancellationToken);
        if (!batch.Succeeded)
        {
            return IntradayRepairExecutionResult.Failed(activeAttempt.MarkFailed(asOfUtc), IntradayRepairState.RepairFetchFailedReasonCode);
        }

        if (ShouldTreatCorrectedRepairAsNoOp(activeAttempt, existingBars, batch))
        {
            await NormalizeMatchingCorrectedBarsAsync(symbol, batch, asOfUtc, cancellationToken);
            var normalizedBars = await LoadBarsForSymbolAsync(symbol, cancellationToken);
            return IntradayRepairExecutionResult.Completed(normalizedBars, null, null);
        }

        var recomputeFromUtc = DetermineRecomputeStart(activeAttempt);

        try
        {
            await UpsertBarsAsync(batch, asOfUtc, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            return IntradayRepairExecutionResult.Failed(activeAttempt.MarkFailed(asOfUtc), IntradayRepairState.RepairPersistenceFailedReasonCode);
        }

        try
        {
            var repairedBars = await LoadBarsForSymbolAsync(symbol, cancellationToken);
            return IntradayRepairExecutionResult.AwaitingRecompute(repairedBars, activeAttempt.MarkAwaitingRecompute(asOfUtc), recomputeFromUtc);
        }
        catch
        {
            return IntradayRepairExecutionResult.Failed(activeAttempt.MarkFailed(asOfUtc), IntradayRepairState.RepairRecomputeFailedReasonCode);
        }
    }

    private static IntradayRepairExecutionResult ExecutePendingRecompute(
        IReadOnlyList<DailyBarView> repairedBars,
        IntradayRepairState repairState,
        IntradaySymbolRuntimeSnapshot? priorSymbolSnapshot,
        Instant asOfUtc)
    {
        try
        {
            var recomputeFromUtc = DetermineRecomputeStart(repairState);
            var recomputedIndicatorState = RecomputeIndicatorState(repairedBars, recomputeFromUtc, priorSymbolSnapshot);
            return IntradayRepairExecutionResult.Completed(repairedBars, recomputeFromUtc, recomputedIndicatorState);
        }
        catch
        {
            return IntradayRepairExecutionResult.Failed(repairState.MarkFailed(asOfUtc), IntradayRepairState.RepairRecomputeFailedReasonCode);
        }
    }

    private static IntradayBarRequest BuildRepairRequest(IntradayRepairState repairState, Instant asOfUtc)
    {
        var requestedBarCount = Math.Max(
            IntradayRequiredBarCount,
            (int)Math.Ceiling((asOfUtc - repairState.EarliestAffectedBarUtc).TotalMinutes) + RepairRequestSafetyBufferBars);

        return new IntradayBarRequest(
            repairState.Symbol,
            IntradayInterval,
            repairState.EarliestAffectedBarUtc,
            asOfUtc,
            requestedBarCount,
            HistoricalFeed);
    }

    private static Instant DetermineRecomputeStart(IntradayRepairState repairState) =>
        repairState.EarliestAffectedBarUtc;

    private static bool ShouldTreatCorrectedRepairAsNoOp(
        IntradayRepairState repairState,
        IReadOnlyList<DailyBarView> existingBars,
        HistoricalBarBatchResult batch)
    {
        if (repairState.CauseCodes.Any(causeCode => string.Equals(causeCode, IntradayRepairState.GapInternalReasonCode, StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(causeCode, IntradayRepairState.GapTrailingReasonCode, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!repairState.CauseCodes.Contains(IntradayRepairState.CorrectedFinalizedBarReasonCode, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var existingByBarTime = existingBars.ToDictionary(x => x.BarTimeUtc, x => x);
        var candidateBars = batch.Bars
            .Where(x => x.BarTimeUtc >= repairState.EarliestAffectedBarUtc)
            .OrderBy(x => x.BarTimeUtc)
            .ToArray();

        if (candidateBars.Length == 0)
        {
            return false;
        }

        return candidateBars.All(bar => existingByBarTime.TryGetValue(bar.BarTimeUtc, out var existing) && AreMateriallyEquivalent(existing, bar));
    }

    private async Task NormalizeMatchingCorrectedBarsAsync(string symbol, HistoricalBarBatchResult batch, Instant asOfUtc, CancellationToken cancellationToken)
    {
        var candidateBarTimes = batch.Bars.Select(x => x.BarTimeUtc).ToArray();
        if (candidateBarTimes.Length == 0)
        {
            return;
        }

        var rows = await dbContext.Bars
            .Where(x => x.Symbol == symbol && x.Interval == IntradayInterval && candidateBarTimes.Contains(x.BarTimeUtc))
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            var providerBar = batch.Bars.First(x => x.BarTimeUtc == row.BarTimeUtc);
            row.ProviderName = batch.ProviderName;
            row.ProviderFeed = batch.ProviderFeed ?? string.Empty;
            row.RuntimeState = providerBar.RuntimeState;
            row.IsReconciled = providerBar.IsReconciled;
            row.UpdatedUtc = asOfUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<DailyBarView>> LoadBarsForSymbolAsync(string symbol, CancellationToken cancellationToken) =>
        await dbContext.Bars
            .AsNoTracking()
            .Where(x => x.Symbol == symbol && x.Interval == IntradayInterval)
            .OrderBy(x => x.BarTimeUtc)
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
            .ToArrayAsync(cancellationToken);

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
                continue;
            }

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

    private IntradayUniverseRuntimeSnapshot BuildUniverseSnapshot(
        IReadOnlyList<string> symbols,
        IReadOnlyDictionary<string, IReadOnlyList<DailyBarView>> grouped,
        Instant asOfUtc,
        string? overrideReadinessState,
        string? overrideReasonCode,
        IReadOnlyDictionary<string, SnapshotComputationOverride>? perSymbolOverrides = null)
    {
        var snapshots = symbols
            .Select(symbol => BuildSnapshot(
                symbol,
                grouped.TryGetValue(symbol, out var bars) ? bars : [],
                asOfUtc,
                perSymbolOverrides is not null && perSymbolOverrides.TryGetValue(symbol, out var symbolOverride) ? symbolOverride : null))
            .ToArray();

        var rollupReadiness = overrideReadinessState ?? DetermineRollupReadinessState(snapshots);
        var rollupReason = overrideReasonCode ?? DetermineRollupReasonCode(snapshots, rollupReadiness);
        return new IntradayUniverseRuntimeSnapshot(IntradayInterval, IntradayCoreProfileKey, asOfUtc, rollupReadiness, rollupReason, snapshots);
    }

    private static string DetermineRollupReadinessState(IReadOnlyList<IntradaySymbolRuntimeSnapshot> snapshots)
    {
        if (snapshots.Any(x => x.ReadinessState == "not_ready"))
        {
            return "not_ready";
        }

        if (snapshots.Any(x => x.ReadinessState == IntradayRepairState.RepairingState))
        {
            return IntradayRepairState.RepairingState;
        }

        return "ready";
    }

    private static string DetermineRollupReasonCode(IReadOnlyList<IntradaySymbolRuntimeSnapshot> snapshots, string rollupReadiness)
    {
        if (rollupReadiness == "ready")
        {
            return "none";
        }

        return snapshots.FirstOrDefault(x => x.ReadinessState == rollupReadiness)?.ReasonCode
               ?? snapshots.FirstOrDefault(x => x.ReadinessState != "ready")?.ReasonCode
               ?? "none";
    }

    private static IReadOnlyList<DailyBarView> FilterToCurrentAndPriorSession(IReadOnlyList<DailyBarView> bars)
    {
        var marketDates = bars.Select(x => x.MarketDate).Distinct().OrderByDescending(x => x).Take(2).ToArray();
        return bars.Where(x => marketDates.Contains(x.MarketDate)).ToArray();
    }

    private static IntradaySymbolRuntimeSnapshot BuildSnapshot(string symbol, IReadOnlyList<DailyBarView> allBars, Instant asOfUtc, SnapshotComputationOverride? snapshotOverride = null)
    {
        var runtimeBars = FilterToCurrentAndPriorSession(allBars);
        var repairAssessment = BuildRepairAssessment(symbol, runtimeBars, asOfUtc, snapshotOverride?.ActiveRepair);
        var activeRepair = snapshotOverride?.ActiveRepair ?? repairAssessment.ActiveRepair;
        var indicatorState = snapshotOverride?.IndicatorState ?? BuildIndicatorState(allBars, activeRepair is not null, snapshotOverride?.RecomputedFromUtc);
        var availableBarCount = runtimeBars.Count;
        var hasRequiredBars = availableBarCount >= IntradayRequiredBarCount;
        var readinessState = !hasRequiredBars
            ? "not_ready"
            : activeRepair is not null
                ? IntradayRepairState.RepairingState
                : indicatorState.HasRequiredIndicatorState ? "ready" : "not_ready";
        var reasonCode = !hasRequiredBars
            ? "missing_required_intraday_bars"
            : activeRepair is not null
                ? snapshotOverride?.ReasonCode ?? activeRepair.PrimaryReasonCode
            : indicatorState.VolumeBuzzReferenceState.HasRequiredReferenceHistory
                ? indicatorState.HasRequiredIndicatorState ? "none" : IntradayRepairState.AwaitingRecomputeReasonCode
                : "insufficient_volume_buzz_reference_history";

        return new IntradaySymbolRuntimeSnapshot(
            symbol,
            IntradayInterval,
            IntradayCoreProfileKey,
            IntradayRequiredBarCount,
            availableBarCount,
            runtimeBars.LastOrDefault()?.BarTimeUtc,
            readinessState,
            reasonCode,
            asOfUtc,
            indicatorState,
            runtimeBars,
            repairAssessment.GapState.ActiveGapType,
            repairAssessment.GapState.ActiveGapStartUtc,
            activeRepair);
    }

    private static IntradayComputedIndicatorState BuildIndicatorState(IReadOnlyList<DailyBarView> allBars, bool hasActiveRepair, Instant? recomputedFromUtc)
    {
        var replayState = BuildReplayState(allBars);
        var (ema30, ema100, vwap) = ExtractRuntimeIndicators(replayState);
        var volumeBuzzReferenceState = BuildVolumeBuzzReferenceState(allBars, replayState.SessionVolumeCurves);
        var hasRequiredIndicatorState = !hasActiveRepair
                                         && ema30.HasValue
                                         && ema100.HasValue
                                         && volumeBuzzReferenceState.HasRequiredReferenceHistory
                                         && vwap.HasValue;

        return new IntradayComputedIndicatorState(
            ema30,
            ema100,
            volumeBuzzReferenceState.HasRequiredReferenceHistory ? CalculateVolumeBuzzPercent(volumeBuzzReferenceState) : null,
            vwap,
            recomputedFromUtc,
            null,
            hasRequiredIndicatorState,
            volumeBuzzReferenceState,
            replayState);
    }

    private static IntradayComputedIndicatorState RecomputeIndicatorState(
        IReadOnlyList<DailyBarView> allBars,
        Instant recomputeFromUtc,
        IntradaySymbolRuntimeSnapshot? priorSymbolSnapshot)
    {
        var runtimeBars = FilterToCurrentAndPriorSession(allBars);
        if (runtimeBars.Count == 0)
        {
            return BuildIndicatorState(allBars, false, recomputeFromUtc);
        }

        var replayStartIndex = DetermineReplayStartIndex(runtimeBars, recomputeFromUtc);
        var replayedBarCount = Math.Max(0, runtimeBars.Count - replayStartIndex);

        if (priorSymbolSnapshot?.IndicatorState.ReplayState is null)
        {
            return BuildIndicatorState(allBars, false, recomputeFromUtc);
        }

        var replayState = BuildReplayState(allBars, recomputeFromUtc, priorSymbolSnapshot.IndicatorState.ReplayState);
        var (ema30, ema100, vwap) = ExtractRuntimeIndicators(replayState);
        var volumeBuzzReferenceState = BuildVolumeBuzzReferenceState(allBars, replayState.SessionVolumeCurves);
        var hasRequiredIndicatorState = ema30.HasValue
                                         && ema100.HasValue
                                         && volumeBuzzReferenceState.HasRequiredReferenceHistory
                                         && vwap.HasValue;

        return new IntradayComputedIndicatorState(
            ema30,
            ema100,
            volumeBuzzReferenceState.HasRequiredReferenceHistory ? CalculateVolumeBuzzPercent(volumeBuzzReferenceState) : null,
            vwap,
            recomputeFromUtc,
            replayedBarCount,
            hasRequiredIndicatorState,
            volumeBuzzReferenceState,
            replayState);
    }

    private static IntradayRepairAssessment BuildRepairAssessment(
        string symbol,
        IReadOnlyList<DailyBarView> runtimeBars,
        Instant asOfUtc,
        IntradayRepairState? priorActiveRepair)
    {
        var gapState = BuildGapState(runtimeBars, asOfUtc);

        if (runtimeBars.Count < IntradayRequiredBarCount)
        {
            return new IntradayRepairAssessment(gapState, null);
        }

        List<IntradayRepairTrigger> triggers = [];

        if (gapState.ReasonCode is not null && gapState.ActiveGapStartUtc.HasValue)
        {
            triggers.Add(new IntradayRepairTrigger(
                gapState.ReasonCode,
                gapState.ActiveGapStartUtc.Value,
                IntradayRepairState.HighPriorityTier));
        }

        var correctedBarUtc = runtimeBars
            .Where(IsCorrectedFinalizedBar)
            .Select(bar => bar.BarTimeUtc)
            .OrderBy(barTimeUtc => barTimeUtc)
            .FirstOrDefault();

        if (correctedBarUtc != default)
        {
            triggers.Add(new IntradayRepairTrigger(
                IntradayRepairState.CorrectedFinalizedBarReasonCode,
                correctedBarUtc,
                IntradayRepairState.NormalPriorityTier));
        }

        // A symbol/interval/profile has at most one active repair job; repeated detections widen the earliest affected bar instead of forking duplicate work.
        var detectedRepair = IntradayRepairState.Create(symbol, IntradayInterval, IntradayCoreProfileKey, triggers, asOfUtc);
        var activeRepair = detectedRepair is null
            ? priorActiveRepair?.PendingRecompute == true ? priorActiveRepair : null
            : priorActiveRepair is null
                ? detectedRepair
                : priorActiveRepair.MergeDetectedRepair(detectedRepair, asOfUtc);

        return new IntradayRepairAssessment(gapState, activeRepair);
    }

    private static bool ValidateRepairedSequence(IReadOnlyList<DailyBarView> runtimeBars, Instant asOfUtc)
    {
        var gapState = BuildGapState(runtimeBars, asOfUtc);
        return !gapState.HasGap && runtimeBars.All(bar => !IsCorrectedFinalizedBar(bar));
    }

    private static bool IsCorrectedFinalizedBar(DailyBarView bar) =>
        string.Equals(bar.RuntimeState, "corrected", StringComparison.OrdinalIgnoreCase)
        || !bar.IsReconciled;

    private static bool AreMateriallyEquivalent(DailyBarView existing, HistoricalBarRecord incoming) =>
        existing.Open == incoming.Open
        && existing.High == incoming.High
        && existing.Low == incoming.Low
        && existing.Close == incoming.Close
        && existing.Volume == incoming.Volume
        && string.Equals(existing.SessionType, incoming.SessionType, StringComparison.OrdinalIgnoreCase)
        && existing.MarketDate == incoming.MarketDate;

    private static IntradayGapState BuildGapState(IReadOnlyList<DailyBarView> runtimeBars, Instant asOfUtc)
    {
        if (runtimeBars.Count == 0)
        {
            return IntradayGapState.None;
        }

        var retainedSessions = runtimeBars
            .GroupBy(x => x.MarketDate)
            .OrderBy(x => x.Key)
            .Select(group => group.OrderBy(x => x.BarTimeUtc).ToArray())
            .ToArray();

        if (retainedSessions.Length == 0)
        {
            return IntradayGapState.None;
        }

        var latestRetainedDate = retainedSessions[^1][0].MarketDate;
        var expectedBarTimes = retainedSessions
            .SelectMany(sessionBars => BuildExpectedBarTimes(sessionBars, latestRetainedDate, asOfUtc))
            .OrderBy(x => x)
            .ToArray();

        if (expectedBarTimes.Length == 0)
        {
            return IntradayGapState.None;
        }

        var actualBarTimes = runtimeBars
            .Select(x => x.BarTimeUtc)
            .ToHashSet();

        var missingBarTimes = expectedBarTimes
            .Where(barTime => !actualBarTimes.Contains(barTime))
            .ToArray();

        if (missingBarTimes.Length == 0)
        {
            return IntradayGapState.None;
        }

        var firstMissingIndex = Array.FindIndex(expectedBarTimes, barTime => !actualBarTimes.Contains(barTime));
        var trailingGap = firstMissingIndex >= 0
                          && expectedBarTimes
                              .Skip(firstMissingIndex)
                              .All(barTime => !actualBarTimes.Contains(barTime));

        return new IntradayGapState(
            trailingGap ? "trailing" : "internal",
            trailingGap ? IntradayRepairState.GapTrailingReasonCode : IntradayRepairState.GapInternalReasonCode,
            missingBarTimes[0]);
    }

    private static IReadOnlyList<Instant> BuildExpectedBarTimes(IReadOnlyList<DailyBarView> sessionBars, LocalDate latestRetainedDate, Instant asOfUtc)
    {
        if (sessionBars.Count == 0)
        {
            return [];
        }

        var marketDate = sessionBars[0].MarketDate;
        var sessionOpenUtc = sessionBars[0].BarTimeUtc;
        var sessionCloseUtc = sessionOpenUtc + Duration.FromMinutes(IntradaySessionBarCount);
        var expectedEndExclusiveUtc = marketDate == latestRetainedDate
            ? DetermineCurrentSessionEndExclusive(sessionBars, marketDate, sessionCloseUtc, asOfUtc)
            : sessionCloseUtc;

        if (expectedEndExclusiveUtc <= sessionOpenUtc)
        {
            return [];
        }

        var expectedBarCount = (int)((expectedEndExclusiveUtc - sessionOpenUtc).TotalMinutes);
        return Enumerable.Range(0, expectedBarCount)
            .Select(offset => sessionOpenUtc + Duration.FromMinutes(offset))
            .ToArray();
    }

    private static Instant DetermineCurrentSessionEndExclusive(IReadOnlyList<DailyBarView> sessionBars, LocalDate marketDate, Instant sessionCloseUtc, Instant asOfUtc)
    {
        var currentMarketDate = asOfUtc.InZone(MarketTimeZone).Date;
        if (marketDate < currentMarketDate)
        {
            return sessionCloseUtc;
        }

        var lastObservedBarUtc = sessionBars[^1].BarTimeUtc + Duration.FromMinutes(1);
        if (asOfUtc >= sessionCloseUtc)
        {
            return sessionCloseUtc;
        }

        return MinInstant(sessionCloseUtc, MaxInstant(lastObservedBarUtc, FloorToMinute(asOfUtc)));
    }

    private static Instant FloorToMinute(Instant instant)
    {
        var secondsSinceEpoch = instant.ToUnixTimeSeconds();
        return Instant.FromUnixTimeSeconds(secondsSinceEpoch - (secondsSinceEpoch % 60));
    }

    private static Instant MinInstant(Instant left, Instant right) => left <= right ? left : right;

    private static Instant MaxInstant(Instant left, Instant right) => left >= right ? left : right;

    private static IntradayIndicatorReplayState BuildReplayState(IReadOnlyList<DailyBarView> allBars)
    {
        var runtimeBars = FilterToCurrentAndPriorSession(allBars).OrderBy(bar => bar.BarTimeUtc).ToArray();
        var (runtimeReplayPoints, replayedRuntimeBarCount) = BuildRuntimeReplayPoints(runtimeBars, 0, null);
        var orderedSessions = allBars
            .GroupBy(x => x.MarketDate)
            .OrderBy(x => x.Key)
            .Select(group => group.OrderBy(x => x.BarTimeUtc).ToArray())
            .ToArray();
        var volumeCurveSteps = 0;
        var sessionCurves = orderedSessions
            .Select(sessionBars =>
            {
                var (curve, replayedBarCount) = BuildSessionVolumeCurve(sessionBars, 0, null);
                volumeCurveSteps += replayedBarCount;
                return new IntradaySessionVolumeCurve(sessionBars[0].MarketDate, curve);
            })
            .ToArray();

        return new IntradayIndicatorReplayState(
            runtimeReplayPoints,
            sessionCurves,
            new IntradayIndicatorReplayExecution(replayedRuntimeBarCount, replayedRuntimeBarCount, replayedRuntimeBarCount, volumeCurveSteps));
    }

    private static IntradayIndicatorReplayState BuildReplayState(
        IReadOnlyList<DailyBarView> allBars,
        Instant recomputeFromUtc,
        IntradayIndicatorReplayState priorReplayState)
    {
        var runtimeBars = FilterToCurrentAndPriorSession(allBars).OrderBy(bar => bar.BarTimeUtc).ToArray();
        var replayStartIndex = DetermineReplayStartIndex(runtimeBars, recomputeFromUtc);
        if (!CanReplayRuntimeFromSeed(runtimeBars, replayStartIndex, priorReplayState.RuntimeReplayPoints))
        {
            return BuildReplayState(allBars);
        }

        var (runtimeReplayPoints, replayedRuntimeBarCount) = BuildRuntimeReplayPoints(runtimeBars, replayStartIndex, priorReplayState.RuntimeReplayPoints);
        var recomputeMarketDate = recomputeFromUtc.InUtc().Date;
        var priorCurvesByDate = priorReplayState.SessionVolumeCurves.ToDictionary(x => x.MarketDate);
        var orderedSessions = allBars
            .GroupBy(x => x.MarketDate)
            .OrderBy(x => x.Key)
            .Select(group => group.OrderBy(x => x.BarTimeUtc).ToArray())
            .ToArray();

        var volumeCurveSteps = 0;
        var sessionCurves = new List<IntradaySessionVolumeCurve>(orderedSessions.Length);
        foreach (var sessionBars in orderedSessions)
        {
            if (sessionBars.Length == 0)
            {
                continue;
            }

            var marketDate = sessionBars[0].MarketDate;
            if (marketDate != recomputeMarketDate)
            {
                // Earlier sessions are outside the affected replay window; preserving their seeded curves keeps repair work bounded to the changed suffix.
                if (!priorCurvesByDate.TryGetValue(marketDate, out var preservedCurve)
                    || preservedCurve.CumulativeVolumes.Count != sessionBars.Length)
                {
                    return BuildReplayState(allBars);
                }

                sessionCurves.Add(preservedCurve);
                continue;
            }

            priorCurvesByDate.TryGetValue(marketDate, out var priorCurve);
            var sessionReplayStartIndex = DetermineSessionReplayStartIndex(sessionBars, recomputeFromUtc);
            var (curve, replayedBarCount) = BuildSessionVolumeCurve(sessionBars, sessionReplayStartIndex, priorCurve?.CumulativeVolumes);
            volumeCurveSteps += replayedBarCount;
            sessionCurves.Add(new IntradaySessionVolumeCurve(marketDate, curve));
        }

        return new IntradayIndicatorReplayState(
            runtimeReplayPoints,
            sessionCurves,
            new IntradayIndicatorReplayExecution(replayedRuntimeBarCount, replayedRuntimeBarCount, replayedRuntimeBarCount, volumeCurveSteps));
    }

    private static (decimal? Ema30, decimal? Ema100, decimal? Vwap) ExtractRuntimeIndicators(IntradayIndicatorReplayState replayState)
    {
        if (replayState.RuntimeReplayPoints.Count == 0)
        {
            return (null, null, null);
        }

        var lastPoint = replayState.RuntimeReplayPoints[^1];
        decimal? vwap = lastPoint.CumulativeVolume == 0 ? null : lastPoint.CumulativeTypicalPriceVolume / lastPoint.CumulativeVolume;
        return (lastPoint.Ema30, lastPoint.Ema100, vwap);
    }

    private static bool CanReplayRuntimeFromSeed(
        IReadOnlyList<DailyBarView> runtimeBars,
        int replayStartIndex,
        IReadOnlyList<IntradayRuntimeReplayPoint> priorReplayPoints)
    {
        if (replayStartIndex <= 0)
        {
            return true;
        }

        if (priorReplayPoints.Count < replayStartIndex)
        {
            return false;
        }

        if (priorReplayPoints[replayStartIndex - 1].BarTimeUtc != runtimeBars[replayStartIndex - 1].BarTimeUtc)
        {
            return false;
        }

        if (replayStartIndex > 29 && !priorReplayPoints[replayStartIndex - 1].Ema30.HasValue)
        {
            return false;
        }

        if (replayStartIndex > 99 && !priorReplayPoints[replayStartIndex - 1].Ema100.HasValue)
        {
            return false;
        }

        return true;
    }

    private static (IReadOnlyList<IntradayRuntimeReplayPoint> Points, int ReplayedBarCount) BuildRuntimeReplayPoints(
        IReadOnlyList<DailyBarView> runtimeBars,
        int replayStartIndex,
        IReadOnlyList<IntradayRuntimeReplayPoint>? priorReplayPoints)
    {
        if (runtimeBars.Count == 0)
        {
            return ([], 0);
        }

        var normalizedReplayStartIndex = Math.Clamp(replayStartIndex, 0, runtimeBars.Count);
        var replayPoints = new IntradayRuntimeReplayPoint[runtimeBars.Count];
        decimal cumulativeCloseSum = 0;
        decimal? ema30 = null;
        decimal? ema100 = null;
        decimal cumulativeTypicalPriceVolume = 0;
        long cumulativeVolume = 0;

        if (normalizedReplayStartIndex > 0 && priorReplayPoints is not null)
        {
            for (var index = 0; index < normalizedReplayStartIndex; index++)
            {
                replayPoints[index] = priorReplayPoints[index];
            }

            var seed = priorReplayPoints[normalizedReplayStartIndex - 1];
            cumulativeCloseSum = seed.CumulativeCloseSum;
            ema30 = seed.Ema30;
            ema100 = seed.Ema100;
            cumulativeTypicalPriceVolume = seed.CumulativeTypicalPriceVolume;
            cumulativeVolume = seed.CumulativeVolume;
        }

        for (var index = normalizedReplayStartIndex; index < runtimeBars.Count; index++)
        {
            var bar = runtimeBars[index];
            cumulativeCloseSum += bar.Close;
            ema30 = AdvanceEma(index, 30, cumulativeCloseSum, bar.Close, ema30);
            ema100 = AdvanceEma(index, 100, cumulativeCloseSum, bar.Close, ema100);

            var typicalPrice = (bar.High + bar.Low + bar.Close) / 3m;
            cumulativeTypicalPriceVolume += typicalPrice * bar.Volume;
            cumulativeVolume += bar.Volume;

            replayPoints[index] = new IntradayRuntimeReplayPoint(
                bar.BarTimeUtc,
                cumulativeCloseSum,
                ema30,
                ema100,
                cumulativeTypicalPriceVolume,
                cumulativeVolume);
        }

        return (replayPoints, runtimeBars.Count - normalizedReplayStartIndex);
    }

    private static decimal? AdvanceEma(int barIndex, int period, decimal cumulativeCloseSum, decimal close, decimal? priorEma)
    {
        if (barIndex + 1 < period)
        {
            return null;
        }

        if (barIndex + 1 == period)
        {
            return cumulativeCloseSum / period;
        }

        if (!priorEma.HasValue)
        {
            throw new InvalidOperationException($"Missing EMA seed for replay at bar index {barIndex}.");
        }

        var multiplier = 2m / (period + 1m);
        return ((close - priorEma.Value) * multiplier) + priorEma.Value;
    }

    private static (IReadOnlyList<long> Curve, int ReplayedBarCount) BuildSessionVolumeCurve(
        IReadOnlyList<DailyBarView> sessionBars,
        int replayStartIndex,
        IReadOnlyList<long>? priorCurve)
    {
        var cumulativeCurve = new long[sessionBars.Count];
        var normalizedReplayStartIndex = Math.Clamp(replayStartIndex, 0, sessionBars.Count);
        long runningVolume = 0;

        if (normalizedReplayStartIndex > 0 && priorCurve is not null)
        {
            if (priorCurve.Count < normalizedReplayStartIndex)
            {
                throw new InvalidOperationException("Missing cumulative-volume seed for replay.");
            }

            for (var index = 0; index < normalizedReplayStartIndex; index++)
            {
                cumulativeCurve[index] = priorCurve[index];
            }

            runningVolume = priorCurve[normalizedReplayStartIndex - 1];
        }

        for (var index = normalizedReplayStartIndex; index < sessionBars.Count; index++)
        {
            runningVolume += sessionBars[index].Volume;
            cumulativeCurve[index] = runningVolume;
        }

        return (cumulativeCurve, sessionBars.Count - normalizedReplayStartIndex);
    }

    private static IntradayVolumeBuzzReferenceState BuildVolumeBuzzReferenceState(
        IReadOnlyList<DailyBarView> bars,
        IReadOnlyList<IntradaySessionVolumeCurve> sessionVolumeCurves)
    {
        if (bars.Count == 0)
        {
            return new IntradayVolumeBuzzReferenceState(VolumeBuzzReferenceSessionCount, 0, null, null, null, []);
        }

        var orderedSessions = bars
            .GroupBy(x => x.MarketDate)
            .OrderBy(x => x.Key)
            .Select(group => group.OrderBy(x => x.BarTimeUtc).ToArray())
            .ToArray();

        if (orderedSessions.Length == 0)
        {
            return new IntradayVolumeBuzzReferenceState(VolumeBuzzReferenceSessionCount, 0, null, null, null, []);
        }

        var curvesByDate = sessionVolumeCurves.ToDictionary(x => x.MarketDate, x => x.CumulativeVolumes);
        var currentSession = orderedSessions[^1];
        var currentSessionOffset = currentSession.Length - 1;
        if (currentSessionOffset < 0 || !curvesByDate.TryGetValue(currentSession[0].MarketDate, out var currentSessionCurve) || currentSessionCurve.Count == 0)
        {
            return new IntradayVolumeBuzzReferenceState(VolumeBuzzReferenceSessionCount, 0, null, null, null, []);
        }

        var currentSessionCumulativeVolume = currentSessionCurve[^1];
        var historicalCurves = orderedSessions
            .Take(Math.Max(0, orderedSessions.Length - 1))
            .Reverse()
            .Select(sessionBars => curvesByDate.TryGetValue(sessionBars[0].MarketDate, out var curve) ? curve : [])
            .Where(curve => curve.Count > currentSessionOffset)
            .Take(VolumeBuzzReferenceSessionCount)
            .Select(curve => (IReadOnlyList<long>)curve)
            .ToArray();

        decimal? historicalAverage = historicalCurves.Length == 0
            ? null
            : historicalCurves.Average(curve => (decimal)curve[currentSessionOffset]);

        return new IntradayVolumeBuzzReferenceState(
            VolumeBuzzReferenceSessionCount,
            historicalCurves.Length,
            currentSessionOffset,
            currentSessionCumulativeVolume,
            historicalAverage,
            historicalCurves);
    }

    private static decimal? CalculateVolumeBuzzPercent(IntradayVolumeBuzzReferenceState referenceState)
    {
        if (!referenceState.CurrentSessionCumulativeVolume.HasValue
            || !referenceState.HistoricalAverageCumulativeVolumeAtOffset.HasValue
            || referenceState.HistoricalAverageCumulativeVolumeAtOffset.Value == 0)
        {
            return null;
        }

        return (referenceState.CurrentSessionCumulativeVolume.Value / referenceState.HistoricalAverageCumulativeVolumeAtOffset.Value) * 100m;
    }

    private sealed record SnapshotComputationOverride(
        IntradayRepairState? ActiveRepair,
        string? ReasonCode,
        Instant? RecomputedFromUtc,
        IntradayComputedIndicatorState? IndicatorState);

    private sealed record RepairCandidate(
        string Symbol,
        IReadOnlyList<DailyBarView> ExistingBars,
        IntradaySymbolRuntimeSnapshot? PriorSnapshot,
        IntradayRepairState ActiveRepair);

    private static HashSet<string> SelectRepairExecutionSymbols(IReadOnlyList<RepairCandidate> candidates, Instant asOfUtc)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var maxConcurrentJobs = candidates.Min(candidate => candidate.ActiveRepair.MaxConcurrentJobs);

        return candidates
            .Where(candidate => candidate.ActiveRepair.CanStartAttempt(asOfUtc))
            .OrderByDescending(candidate => GetRepairPriorityRank(candidate.ActiveRepair.PriorityTier))
            .ThenBy(candidate => candidate.ActiveRepair.EarliestAffectedBarUtc)
            .ThenBy(candidate => candidate.Symbol, StringComparer.OrdinalIgnoreCase)
            .Take(maxConcurrentJobs)
            .Select(candidate => candidate.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int GetRepairPriorityRank(string priorityTier) =>
        string.Equals(priorityTier, IntradayRepairState.HighPriorityTier, StringComparison.OrdinalIgnoreCase) ? 1 : 0;

    private static int DetermineReplayStartIndex(IReadOnlyList<DailyBarView> runtimeBars, Instant recomputeFromUtc)
    {
        var runtimeArray = runtimeBars.ToArray();
        var replayStartIndex = Array.FindIndex(runtimeArray, bar => bar.BarTimeUtc >= recomputeFromUtc);
        return replayStartIndex < 0 ? runtimeArray.Length : replayStartIndex;
    }

    private static int DetermineSessionReplayStartIndex(IReadOnlyList<DailyBarView> sessionBars, Instant recomputeFromUtc)
    {
        if (sessionBars.Count == 0)
        {
            return 0;
        }

        if (recomputeFromUtc <= sessionBars[0].BarTimeUtc)
        {
            return 0;
        }

        if (recomputeFromUtc > sessionBars[^1].BarTimeUtc)
        {
            return sessionBars.Count;
        }

        return DetermineReplayStartIndex(sessionBars, recomputeFromUtc);
    }
}
