using NodaTime;

namespace Aegis.MarketData.Application;

public sealed record IntradayRuntimeReplayPoint(
    Instant BarTimeUtc,
    decimal CumulativeCloseSum,
    decimal? Ema30,
    decimal? Ema100,
    decimal CumulativeTypicalPriceVolume,
    long CumulativeVolume);

public sealed record IntradaySessionVolumeCurve(
    LocalDate MarketDate,
    IReadOnlyList<long> CumulativeVolumes);

public sealed record IntradayIndicatorReplayExecution(
    int Ema30Steps,
    int Ema100Steps,
    int VwapSteps,
    int VolumeCurveSteps);

public sealed record IntradayIndicatorReplayState(
    IReadOnlyList<IntradayRuntimeReplayPoint> RuntimeReplayPoints,
    IReadOnlyList<IntradaySessionVolumeCurve> SessionVolumeCurves,
    IntradayIndicatorReplayExecution Execution);

public sealed record IntradayComputedIndicatorState(
    decimal? Ema30,
    decimal? Ema100,
    decimal? VolumeBuzzPercent,
    decimal? Vwap,
    Instant? RecomputedFromUtc,
    int? ReplayedBarCount,
    bool HasRequiredIndicatorState,
    IntradayVolumeBuzzReferenceState VolumeBuzzReferenceState,
    IntradayIndicatorReplayState ReplayState);
