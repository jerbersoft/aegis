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
        state.PendingRecompute.ShouldBeTrue();
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
}
