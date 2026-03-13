namespace Aegis.MarketData.Application;

internal sealed record IntradayRepairAssessment(
    IntradayGapState GapState,
    IntradayRepairState? ActiveRepair);
