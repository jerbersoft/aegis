using Aegis.Shared.Contracts.MarketData;

namespace Aegis.Backend.MarketData;

public interface IMarketDataRealtimeOrchestrator
{
    Task<MarketDataSubscriptionAck> SubscribeHomeAsync(string connectionId, CancellationToken cancellationToken);

    Task UnsubscribeHomeAsync(string connectionId, CancellationToken cancellationToken);

    Task<MarketDataSubscriptionAck?> SubscribeWatchlistAsync(string connectionId, Guid watchlistId, CancellationToken cancellationToken);

    Task UnsubscribeWatchlistAsync(string connectionId, Guid watchlistId, CancellationToken cancellationToken);

    Task UnsubscribeAllAsync(string connectionId, CancellationToken cancellationToken);

    Task PublishHomeRefreshHintAsync(IReadOnlyList<string> changedScopes, CancellationToken cancellationToken);

    Task FlushPendingHomeRefreshHintAsync(CancellationToken cancellationToken);

    Task PublishWatchlistSnapshotAsync(Guid watchlistId, CancellationToken cancellationToken);

    Task FlushPendingWatchlistSnapshotAsync(Guid watchlistId, CancellationToken cancellationToken);

    Task PublishSubscribedWatchlistsAsync(CancellationToken cancellationToken);
}
