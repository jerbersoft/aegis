using Aegis.Shared.Contracts.MarketData;
using NodaTime;

namespace Aegis.MarketData.Application;

public sealed class MarketDataBootstrapStateStore
{
    private readonly object _gate = new();

    private MarketDataBootstrapStatusView _status = new("not_requested", "none", DailyMarketDataHydrationService.DailyCoreProfileKey, 0, 0, 0, 0, 0, SystemClock.Instance.GetCurrentInstant(), null, [], []);

    public MarketDataBootstrapStatusView GetStatus()
    {
        lock (_gate)
        {
            return _status;
        }
    }

    public void SetStatus(MarketDataBootstrapStatusView status)
    {
        lock (_gate)
        {
            _status = status;
        }
    }
}
