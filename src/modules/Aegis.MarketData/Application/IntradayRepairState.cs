using NodaTime;

namespace Aegis.MarketData.Application;

public sealed record IntradayRepairTrigger(
    string CauseCode,
    Instant EarliestAffectedBarUtc,
    string PriorityTier);

public sealed record IntradayRepairState(
    string JobKey,
    string Symbol,
    string Interval,
    string ProfileKey,
    string PrimaryReasonCode,
    IReadOnlyList<string> CauseCodes,
    string PriorityTier,
    string OrchestrationState,
    Instant EarliestAffectedBarUtc,
    bool PendingRecompute,
    int AttemptCount,
    Instant LastDetectedUtc,
    Instant? LastAttemptStartedUtc,
    Instant? NextEligibleAttemptUtc,
    int MaxConcurrentJobs)
{
    public const string RepairingState = "repairing";
    public const string QueuedOrchestrationState = "queued";
    public const string RunningOrchestrationState = "running";
    public const string RetryBackoffOrchestrationState = "retry_backoff";
    public const string AwaitingRecomputeOrchestrationState = "awaiting_recompute";
    public const string GapTrailingReasonCode = "gap_trailing";
    public const string GapInternalReasonCode = "gap_internal";
    public const string CorrectedFinalizedBarReasonCode = "corrected_finalized_bar";
    public const string AwaitingRecomputeReasonCode = "awaiting_recompute";
    public const string RepairFetchFailedReasonCode = "repair_fetch_failed";
    public const string RepairPersistenceFailedReasonCode = "repair_persistence_failed";
    public const string RepairRecomputeFailedReasonCode = "repair_recompute_failed";
    public const string RepairValidationFailedReasonCode = "repair_validation_failed";
    public const string HighPriorityTier = "high";
    public const string NormalPriorityTier = "normal";
    public const int DefaultMaxConcurrentJobs = 4;
    private static readonly Duration BaseRetryBackoff = Duration.FromMinutes(1);
    private static readonly Duration MaxRetryBackoff = Duration.FromMinutes(15);

    public static IntradayRepairState? Create(
        string symbol,
        string interval,
        string profileKey,
        IEnumerable<IntradayRepairTrigger> triggers,
        Instant detectedAtUtc,
        int maxConcurrentJobs = DefaultMaxConcurrentJobs)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var normalizedInterval = interval.Trim().ToLowerInvariant();
        var normalizedProfileKey = profileKey.Trim().ToLowerInvariant();

        var normalizedTriggers = triggers
            .Where(trigger => !string.IsNullOrWhiteSpace(trigger.CauseCode))
            .GroupBy(trigger => trigger.CauseCode, StringComparer.Ordinal)
            .Select(group => group.OrderBy(trigger => trigger.EarliestAffectedBarUtc).First())
            .OrderByDescending(trigger => GetPriorityRank(trigger.PriorityTier))
            .ThenBy(trigger => trigger.EarliestAffectedBarUtc)
            .ToArray();

        if (normalizedTriggers.Length == 0)
        {
            return null;
        }

        var primaryTrigger = normalizedTriggers[0];
        var earliestAffectedBarUtc = normalizedTriggers.Min(trigger => trigger.EarliestAffectedBarUtc);

        return new IntradayRepairState(
            BuildJobKey(normalizedSymbol, normalizedInterval, normalizedProfileKey),
            normalizedSymbol,
            normalizedInterval,
            normalizedProfileKey,
            primaryTrigger.CauseCode,
            normalizedTriggers.Select(trigger => trigger.CauseCode).ToArray(),
            primaryTrigger.PriorityTier,
            QueuedOrchestrationState,
            earliestAffectedBarUtc,
            false,
            0,
            detectedAtUtc,
            null,
            detectedAtUtc,
            maxConcurrentJobs);
    }

    public bool CanStartAttempt(Instant now) => !NextEligibleAttemptUtc.HasValue || NextEligibleAttemptUtc.Value <= now;

    public IntradayRepairState MergeDetectedRepair(IntradayRepairState detectedRepair, Instant detectedAtUtc)
    {
        if (!string.Equals(JobKey, detectedRepair.JobKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cannot merge repair state for different repair jobs.");
        }

        var mergedTriggers = CauseCodes
            .Select(causeCode => new IntradayRepairTrigger(causeCode, EarliestAffectedBarUtc, PriorityTier))
            .Concat(detectedRepair.CauseCodes.Select(causeCode => new IntradayRepairTrigger(causeCode, detectedRepair.EarliestAffectedBarUtc, detectedRepair.PriorityTier)));
        var merged = Create(Symbol, Interval, ProfileKey, mergedTriggers, detectedAtUtc, MaxConcurrentJobs);
        if (merged is null)
        {
            return this;
        }

        var widenedEarlier = merged.EarliestAffectedBarUtc < EarliestAffectedBarUtc;
        var raisedPriority = GetPriorityRank(merged.PriorityTier) > GetPriorityRank(PriorityTier);

        return merged with
        {
            OrchestrationState = widenedEarlier || raisedPriority ? QueuedOrchestrationState : OrchestrationState,
            PendingRecompute = PendingRecompute,
            AttemptCount = AttemptCount,
            LastAttemptStartedUtc = LastAttemptStartedUtc,
            NextEligibleAttemptUtc = widenedEarlier || raisedPriority ? detectedAtUtc : NextEligibleAttemptUtc,
            LastDetectedUtc = detectedAtUtc
        };
    }

    public IntradayRepairState MarkAttemptStarted(Instant startedAtUtc) =>
        this with
        {
            OrchestrationState = RunningOrchestrationState,
            PendingRecompute = false,
            AttemptCount = AttemptCount + 1,
            LastAttemptStartedUtc = startedAtUtc,
            LastDetectedUtc = startedAtUtc,
            NextEligibleAttemptUtc = null
        };

    public IntradayRepairState MarkAwaitingRecompute(Instant detectedAtUtc) =>
        this with
        {
            OrchestrationState = AwaitingRecomputeOrchestrationState,
            PendingRecompute = true,
            LastDetectedUtc = detectedAtUtc,
            NextEligibleAttemptUtc = detectedAtUtc
        };

    public IntradayRepairState MarkFailed(Instant failedAtUtc) =>
        this with
        {
            OrchestrationState = RetryBackoffOrchestrationState,
            PendingRecompute = false,
            LastDetectedUtc = failedAtUtc,
            NextEligibleAttemptUtc = failedAtUtc + ComputeRetryBackoff(AttemptCount)
        };

    private static Duration ComputeRetryBackoff(int attemptCount)
    {
        if (attemptCount <= 0)
        {
            return BaseRetryBackoff;
        }

        var multiplier = 1 << Math.Min(attemptCount - 1, 4);
        var computed = Duration.FromMinutes(BaseRetryBackoff.TotalMinutes * multiplier);
        return computed > MaxRetryBackoff ? MaxRetryBackoff : computed;
    }

    public static string BuildJobKey(string symbol, string interval, string profileKey) =>
        $"{symbol.Trim().ToUpperInvariant()}|{interval.Trim().ToLowerInvariant()}|{profileKey.Trim().ToLowerInvariant()}";

    private static int GetPriorityRank(string priorityTier) =>
        string.Equals(priorityTier, HighPriorityTier, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
}
