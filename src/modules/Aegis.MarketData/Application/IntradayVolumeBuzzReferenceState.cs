namespace Aegis.MarketData.Application;

public sealed record IntradayVolumeBuzzReferenceState(
    int RequiredReferenceSessionCount,
    int AvailableReferenceSessionCount,
    int? CurrentSessionOffset,
    long? CurrentSessionCumulativeVolume,
    decimal? HistoricalAverageCumulativeVolumeAtOffset,
    IReadOnlyList<IReadOnlyList<long>> HistoricalCumulativeVolumeCurves)
{
    public bool HasRequiredReferenceHistory =>
        CurrentSessionOffset.HasValue
        && CurrentSessionCumulativeVolume.HasValue
        && HistoricalAverageCumulativeVolumeAtOffset.HasValue
        && AvailableReferenceSessionCount >= RequiredReferenceSessionCount;
}
