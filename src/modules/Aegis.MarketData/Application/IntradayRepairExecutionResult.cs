using Aegis.Shared.Contracts.MarketData;
using NodaTime;

namespace Aegis.MarketData.Application;

internal sealed record IntradayRepairExecutionResult(
    bool Succeeded,
    IntradayRepairState? ActiveRepair,
    IReadOnlyList<DailyBarView>? RepairedBars,
    Instant? RecomputedFromUtc,
    IntradayComputedIndicatorState? RecomputedIndicatorState,
    string? FailureReasonCode)
{
    public static IntradayRepairExecutionResult NoRepairRequired() =>
        new(true, null, null, null, null, null);

    public static IntradayRepairExecutionResult AwaitingRecompute(
        IReadOnlyList<DailyBarView> repairedBars,
        IntradayRepairState repairState,
        Instant recomputedFromUtc) =>
        new(false, repairState with { PendingRecompute = true }, repairedBars, recomputedFromUtc, null, IntradayRepairState.AwaitingRecomputeReasonCode);

    public static IntradayRepairExecutionResult Completed(
        IReadOnlyList<DailyBarView> repairedBars,
        Instant? recomputedFromUtc,
        IntradayComputedIndicatorState? recomputedIndicatorState) =>
        new(true, null, repairedBars, recomputedFromUtc, recomputedIndicatorState, null);

    public static IntradayRepairExecutionResult Failed(IntradayRepairState repairState, string failureReasonCode) =>
        new(false, repairState with { PendingRecompute = false }, null, null, null, failureReasonCode);
}
