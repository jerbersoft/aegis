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
    Instant EarliestAffectedBarUtc,
    bool PendingRecompute,
    Instant LastDetectedUtc,
    int MaxConcurrentJobs)
{
    public const string RepairingState = "repairing";
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
            earliestAffectedBarUtc,
            true,
            detectedAtUtc,
            maxConcurrentJobs);
    }

    public static string BuildJobKey(string symbol, string interval, string profileKey) =>
        $"{symbol.Trim().ToUpperInvariant()}|{interval.Trim().ToLowerInvariant()}|{profileKey.Trim().ToLowerInvariant()}";

    private static int GetPriorityRank(string priorityTier) =>
        string.Equals(priorityTier, HighPriorityTier, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
}
