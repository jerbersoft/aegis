using Aegis.MarketData.Application;
using Shouldly;
using NodaTime;

namespace Aegis.MarketData.UnitTests;

public sealed class IntradayRepairStateTests
{
    [Fact]
    public void Create_ShouldDeduplicateByCauseAndKeepSingleJobIdentity()
    {
        var detectedAt = Instant.FromUtc(2026, 3, 13, 14, 0);

        var state = IntradayRepairState.Create(
            " aapl ",
            "1MIN",
            "INTRADAY_CORE",
            [
                new IntradayRepairTrigger(IntradayRepairState.GapInternalReasonCode, Instant.FromUtc(2026, 3, 13, 13, 10), IntradayRepairState.HighPriorityTier),
                new IntradayRepairTrigger(IntradayRepairState.GapInternalReasonCode, Instant.FromUtc(2026, 3, 13, 13, 5), IntradayRepairState.HighPriorityTier),
                new IntradayRepairTrigger(IntradayRepairState.CorrectedFinalizedBarReasonCode, Instant.FromUtc(2026, 3, 13, 13, 20), IntradayRepairState.NormalPriorityTier)
            ],
            detectedAt);

        state.ShouldNotBeNull();
        state.JobKey.ShouldBe("AAPL|1min|intraday_core");
        state.CauseCodes.ShouldBe([IntradayRepairState.GapInternalReasonCode, IntradayRepairState.CorrectedFinalizedBarReasonCode]);
        state.EarliestAffectedBarUtc.ShouldBe(Instant.FromUtc(2026, 3, 13, 13, 5));
        state.PrimaryReasonCode.ShouldBe(IntradayRepairState.GapInternalReasonCode);
        state.PriorityTier.ShouldBe(IntradayRepairState.HighPriorityTier);
        state.OrchestrationState.ShouldBe(IntradayRepairState.QueuedOrchestrationState);
        state.PendingRecompute.ShouldBeFalse();
        state.AttemptCount.ShouldBe(0);
        state.NextEligibleAttemptUtc.ShouldBe(detectedAt);
        state.MaxConcurrentJobs.ShouldBe(IntradayRepairState.DefaultMaxConcurrentJobs);
    }

    [Fact]
    public void Create_ShouldPreferHighPriorityTrigger_WhenCorrectedBarIsEarlier()
    {
        var state = IntradayRepairState.Create(
            "MSFT",
            "1min",
            "intraday_core",
            [
                new IntradayRepairTrigger(IntradayRepairState.CorrectedFinalizedBarReasonCode, Instant.FromUtc(2026, 3, 13, 12, 0), IntradayRepairState.NormalPriorityTier),
                new IntradayRepairTrigger(IntradayRepairState.GapTrailingReasonCode, Instant.FromUtc(2026, 3, 13, 13, 0), IntradayRepairState.HighPriorityTier)
            ],
            Instant.FromUtc(2026, 3, 13, 14, 0));

        state.ShouldNotBeNull();
        state.PrimaryReasonCode.ShouldBe(IntradayRepairState.GapTrailingReasonCode);
        state.PriorityTier.ShouldBe(IntradayRepairState.HighPriorityTier);
        state.EarliestAffectedBarUtc.ShouldBe(Instant.FromUtc(2026, 3, 13, 12, 0));
    }

    [Fact]
    public void MergeDetectedRepair_ShouldWidenEarliestAffectedBar_WithoutChangingJobIdentity()
    {
        var detectedAt = Instant.FromUtc(2026, 3, 13, 14, 0);
        var existing = IntradayRepairState.Create(
            "AAPL",
            "1min",
            "intraday_core",
            [new IntradayRepairTrigger(IntradayRepairState.CorrectedFinalizedBarReasonCode, Instant.FromUtc(2026, 3, 13, 13, 30), IntradayRepairState.NormalPriorityTier)],
            detectedAt)!.MarkAttemptStarted(detectedAt);

        var widened = existing.MergeDetectedRepair(
            IntradayRepairState.Create(
                "AAPL",
                "1min",
                "intraday_core",
                [new IntradayRepairTrigger(IntradayRepairState.GapInternalReasonCode, Instant.FromUtc(2026, 3, 13, 13, 10), IntradayRepairState.HighPriorityTier)],
                detectedAt + Duration.FromMinutes(1))!,
            detectedAt + Duration.FromMinutes(1));

        widened.JobKey.ShouldBe(existing.JobKey);
        widened.EarliestAffectedBarUtc.ShouldBe(Instant.FromUtc(2026, 3, 13, 13, 10));
        widened.PrimaryReasonCode.ShouldBe(IntradayRepairState.GapInternalReasonCode);
        widened.PriorityTier.ShouldBe(IntradayRepairState.HighPriorityTier);
        widened.OrchestrationState.ShouldBe(IntradayRepairState.QueuedOrchestrationState);
        widened.AttemptCount.ShouldBe(1);
        widened.NextEligibleAttemptUtc.ShouldBe(detectedAt + Duration.FromMinutes(1));
    }

    [Fact]
    public void MarkFailed_ShouldMoveRepairIntoRetryBackoff()
    {
        var startedAt = Instant.FromUtc(2026, 3, 13, 14, 0);
        var state = IntradayRepairState.Create(
            "AAPL",
            "1min",
            "intraday_core",
            [new IntradayRepairTrigger(IntradayRepairState.GapTrailingReasonCode, Instant.FromUtc(2026, 3, 13, 13, 55), IntradayRepairState.HighPriorityTier)],
            startedAt)!
            .MarkAttemptStarted(startedAt)
            .MarkFailed(startedAt + Duration.FromMinutes(2));

        state.OrchestrationState.ShouldBe(IntradayRepairState.RetryBackoffOrchestrationState);
        state.PendingRecompute.ShouldBeFalse();
        state.AttemptCount.ShouldBe(1);
        state.CanStartAttempt(startedAt + Duration.FromMinutes(2)).ShouldBeFalse();
        state.CanStartAttempt(startedAt + Duration.FromMinutes(3)).ShouldBeTrue();
    }
}
