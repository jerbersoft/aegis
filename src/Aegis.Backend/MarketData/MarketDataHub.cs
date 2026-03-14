using Aegis.Shared.Contracts.MarketData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Aegis.Backend.MarketData;

[Authorize]
public sealed class MarketDataHub(IMarketDataRealtimeOrchestrator orchestrator) : Hub
{
    public Task<MarketDataSubscriptionAck> SubscribeHome() =>
        orchestrator.SubscribeHomeAsync(Context.ConnectionId, Context.ConnectionAborted);

    public Task UnsubscribeHome() =>
        orchestrator.UnsubscribeHomeAsync(Context.ConnectionId, Context.ConnectionAborted);

    public async Task<MarketDataSubscriptionAck> SubscribeWatchlist(MarketDataWatchlistSubscriptionRequest request)
    {
        var ack = await orchestrator.SubscribeWatchlistAsync(Context.ConnectionId, request.WatchlistId, Context.ConnectionAborted);
        return ack ?? throw new HubException("watchlist_not_found");
    }

    public Task UnsubscribeWatchlist(MarketDataWatchlistSubscriptionRequest request) =>
        orchestrator.UnsubscribeWatchlistAsync(Context.ConnectionId, request.WatchlistId, Context.ConnectionAborted);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await orchestrator.UnsubscribeAllAsync(Context.ConnectionId, CancellationToken.None);
        await base.OnDisconnectedAsync(exception);
    }
}
