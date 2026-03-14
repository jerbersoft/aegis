using Aegis.Shared.Contracts.MarketData;

namespace Aegis.Backend.MarketData;

public sealed class MarketDataRealtimeNotifier(IMarketDataRealtimeOrchestrator orchestrator)
{
    public async Task PublishMarketDataRefreshAsync(CancellationToken cancellationToken)
    {
        await orchestrator.PublishHomeRefreshHintAsync(
            [
                MarketDataRealtimeContract.ChangeScopes.BootstrapStatus,
                MarketDataRealtimeContract.ChangeScopes.DailyReadiness,
                MarketDataRealtimeContract.ChangeScopes.IntradayReadiness
            ],
            cancellationToken);

        await orchestrator.PublishSubscribedWatchlistsAsync(cancellationToken);
    }
}
