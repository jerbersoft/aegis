namespace Aegis.Backend.MarketData;

public sealed class MarketDataRealtimeOptions
{
    public const string SectionName = "MarketData:Realtime";

    public int HomeRefreshThrottleMilliseconds { get; set; } = 1000;

    public int WatchlistSnapshotThrottleMilliseconds { get; set; } = 750;
}
