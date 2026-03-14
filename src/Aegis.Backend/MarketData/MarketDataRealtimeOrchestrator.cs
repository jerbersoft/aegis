using System.Collections.Concurrent;
using Aegis.Shared.Contracts.MarketData;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Aegis.Backend.MarketData;

public sealed class MarketDataRealtimeOrchestrator(
    IHubContext<MarketDataHub> hubContext,
    IMarketDataWatchlistSnapshotBuilder watchlistSnapshotBuilder,
    IClock clock,
    IOptions<MarketDataRealtimeOptions> options,
    IServiceScopeFactory serviceScopeFactory) : IMarketDataRealtimeOrchestrator
{
    private sealed class WatchlistFlushState
    {
        public Instant NextEligibleUtc { get; set; } = Instant.MinValue;
        public bool HasPendingFlush { get; set; }
        public CancellationTokenSource? FlushCts { get; set; }
    }

    private readonly ConcurrentDictionary<string, byte> _homeConnections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _watchlistConnections = new();
    private readonly ConcurrentDictionary<Guid, long> _watchlistBatchSequences = new();
    private readonly ConcurrentDictionary<Guid, WatchlistFlushState> _watchlistFlushStates = new();
    private readonly object _gate = new();

    private Instant _nextHomeEligibleUtc = Instant.MinValue;
    private IReadOnlySet<string> _pendingHomeScopes = new HashSet<string>(StringComparer.Ordinal);
    private CancellationTokenSource? _homeFlushCts;

    public async Task<MarketDataSubscriptionAck> SubscribeHomeAsync(string connectionId, CancellationToken cancellationToken)
    {
        _homeConnections[connectionId] = 0;
        await hubContext.Groups.AddToGroupAsync(connectionId, MarketDataRealtimeContract.GroupNames.Home, cancellationToken);

        return new MarketDataSubscriptionAck(
            MarketDataRealtimeContract.ContractVersion,
            MarketDataRealtimeContract.ScopeKinds.Home,
            MarketDataRealtimeContract.ScopeKinds.Home,
            MarketDataRealtimeContract.DeliveryStrategies.RefreshHint,
            true,
            clock.GetCurrentInstant());
    }

    public async Task UnsubscribeHomeAsync(string connectionId, CancellationToken cancellationToken)
    {
        _homeConnections.TryRemove(connectionId, out _);
        await hubContext.Groups.RemoveFromGroupAsync(connectionId, MarketDataRealtimeContract.GroupNames.Home, cancellationToken);
    }

    public async Task<MarketDataSubscriptionAck?> SubscribeWatchlistAsync(string connectionId, Guid watchlistId, CancellationToken cancellationToken)
    {
        var connections = _watchlistConnections.GetOrAdd(watchlistId, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        connections[connectionId] = 0;
        await hubContext.Groups.AddToGroupAsync(connectionId, MarketDataRealtimeContract.GroupNames.Watchlist(watchlistId), cancellationToken);

        var batchSequence = _watchlistBatchSequences.AddOrUpdate(watchlistId, 1, (_, current) => current + 1);
        var initialSnapshot = await watchlistSnapshotBuilder.BuildAsync(watchlistId, batchSequence, cancellationToken);
        if (initialSnapshot is null)
        {
            await UnsubscribeWatchlistAsync(connectionId, watchlistId, cancellationToken);
            return null;
        }

        await hubContext.Clients.Client(connectionId)
            .SendAsync(MarketDataRealtimeContract.EventNames.WatchlistSnapshot, initialSnapshot, cancellationToken);

        return new MarketDataSubscriptionAck(
            MarketDataRealtimeContract.ContractVersion,
            MarketDataRealtimeContract.ScopeKinds.Watchlist,
            watchlistId.ToString("D"),
            MarketDataRealtimeContract.DeliveryStrategies.CoalescedSnapshotDelta,
            true,
            clock.GetCurrentInstant());
    }

    public async Task UnsubscribeWatchlistAsync(string connectionId, Guid watchlistId, CancellationToken cancellationToken)
    {
        if (_watchlistConnections.TryGetValue(watchlistId, out var connections))
        {
            connections.TryRemove(connectionId, out _);
            if (connections.IsEmpty)
            {
                _watchlistConnections.TryRemove(watchlistId, out _);
                if (_watchlistFlushStates.TryRemove(watchlistId, out var flushState))
                {
                    CancelWatchlistFlush(flushState);
                }
            }
        }

        await hubContext.Groups.RemoveFromGroupAsync(connectionId, MarketDataRealtimeContract.GroupNames.Watchlist(watchlistId), cancellationToken);
    }

    public async Task UnsubscribeAllAsync(string connectionId, CancellationToken cancellationToken)
    {
        await UnsubscribeHomeAsync(connectionId, cancellationToken);

        var watchlistIds = _watchlistConnections
            .Where(x => x.Value.ContainsKey(connectionId))
            .Select(x => x.Key)
            .ToList();

        foreach (var watchlistId in watchlistIds)
        {
            await UnsubscribeWatchlistAsync(connectionId, watchlistId, cancellationToken);
        }
    }

    public async Task PublishHomeRefreshHintAsync(IReadOnlyList<string> changedScopes, CancellationToken cancellationToken)
    {
        if (_homeConnections.IsEmpty)
        {
            return;
        }

        MarketDataHomeRefreshEvent? payload = null;
        var now = clock.GetCurrentInstant();

        lock (_gate)
        {
            var mergedScopes = _pendingHomeScopes
                .Concat(changedScopes)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (now < _nextHomeEligibleUtc)
            {
                _pendingHomeScopes = new HashSet<string>(mergedScopes, StringComparer.Ordinal);
                ScheduleHomeFlushLocked(now);
                return;
            }

            _pendingHomeScopes = new HashSet<string>(StringComparer.Ordinal);
            CancelHomeFlushLocked();
            _nextHomeEligibleUtc = now + Duration.FromMilliseconds(Math.Max(0, options.Value.HomeRefreshThrottleMilliseconds));
            payload = new MarketDataHomeRefreshEvent(
                MarketDataRealtimeContract.ContractVersion,
                Guid.NewGuid().ToString("N"),
                now,
                true,
                mergedScopes);
        }

        await hubContext.Clients.Group(MarketDataRealtimeContract.GroupNames.Home)
            .SendAsync(MarketDataRealtimeContract.EventNames.HomeRefreshHint, payload, cancellationToken);
    }

    public async Task FlushPendingHomeRefreshHintAsync(CancellationToken cancellationToken)
    {
        MarketDataHomeRefreshEvent? payload = null;

        lock (_gate)
        {
            if (_pendingHomeScopes.Count == 0 || _homeConnections.IsEmpty)
            {
                CancelHomeFlushLocked();
                return;
            }

            var now = clock.GetCurrentInstant();
            if (now < _nextHomeEligibleUtc)
            {
                ScheduleHomeFlushLocked(now);
                return;
            }

            var mergedScopes = _pendingHomeScopes.Order(StringComparer.Ordinal).ToArray();
            _pendingHomeScopes = new HashSet<string>(StringComparer.Ordinal);
            CancelHomeFlushLocked();
            _nextHomeEligibleUtc = now + Duration.FromMilliseconds(Math.Max(0, options.Value.HomeRefreshThrottleMilliseconds));
            payload = new MarketDataHomeRefreshEvent(
                MarketDataRealtimeContract.ContractVersion,
                Guid.NewGuid().ToString("N"),
                now,
                true,
                mergedScopes);
        }

        await hubContext.Clients.Group(MarketDataRealtimeContract.GroupNames.Home)
            .SendAsync(MarketDataRealtimeContract.EventNames.HomeRefreshHint, payload, cancellationToken);
    }

    public async Task PublishWatchlistSnapshotAsync(Guid watchlistId, CancellationToken cancellationToken)
    {
        if (!_watchlistConnections.TryGetValue(watchlistId, out var connections) || connections.IsEmpty)
        {
            return;
        }

        var now = clock.GetCurrentInstant();
        var throttleWindow = Duration.FromMilliseconds(Math.Max(0, options.Value.WatchlistSnapshotThrottleMilliseconds));
        var flushState = _watchlistFlushStates.GetOrAdd(watchlistId, _ => new WatchlistFlushState());
        var shouldPublish = false;

        lock (flushState)
        {
            if (now < flushState.NextEligibleUtc)
            {
                flushState.HasPendingFlush = true;
                ScheduleWatchlistFlushLocked(watchlistId, flushState, now);
            }
            else
            {
                flushState.HasPendingFlush = false;
                CancelWatchlistFlushLocked(flushState);
                flushState.NextEligibleUtc = now + throttleWindow;
                shouldPublish = true;
            }
        }

        if (!shouldPublish)
        {
            return;
        }

        var batchSequence = _watchlistBatchSequences.AddOrUpdate(watchlistId, 1, (_, current) => current + 1);
        var snapshot = await watchlistSnapshotBuilder.BuildAsync(watchlistId, batchSequence, cancellationToken);
        if (snapshot is null)
        {
            return;
        }

        await hubContext.Clients.Group(MarketDataRealtimeContract.GroupNames.Watchlist(watchlistId))
            .SendAsync(MarketDataRealtimeContract.EventNames.WatchlistSnapshot, snapshot, cancellationToken);
    }

    public async Task FlushPendingWatchlistSnapshotAsync(Guid watchlistId, CancellationToken cancellationToken)
    {
        if (!_watchlistConnections.TryGetValue(watchlistId, out var connections) || connections.IsEmpty)
        {
            if (_watchlistFlushStates.TryRemove(watchlistId, out var staleState))
            {
                CancelWatchlistFlush(staleState);
            }

            return;
        }

        var flushState = _watchlistFlushStates.GetOrAdd(watchlistId, _ => new WatchlistFlushState());
        var throttleWindow = Duration.FromMilliseconds(Math.Max(0, options.Value.WatchlistSnapshotThrottleMilliseconds));
        var shouldPublish = false;

        lock (flushState)
        {
            if (!flushState.HasPendingFlush)
            {
                CancelWatchlistFlushLocked(flushState);
                return;
            }

            var now = clock.GetCurrentInstant();
            if (now < flushState.NextEligibleUtc)
            {
                ScheduleWatchlistFlushLocked(watchlistId, flushState, now);
                return;
            }

            flushState.HasPendingFlush = false;
            CancelWatchlistFlushLocked(flushState);
            flushState.NextEligibleUtc = now + throttleWindow;
            shouldPublish = true;
        }

        if (!shouldPublish)
        {
            return;
        }

        var batchSequence = _watchlistBatchSequences.AddOrUpdate(watchlistId, 1, (_, current) => current + 1);
        var snapshot = await watchlistSnapshotBuilder.BuildAsync(watchlistId, batchSequence, cancellationToken);
        if (snapshot is null)
        {
            return;
        }

        await hubContext.Clients.Group(MarketDataRealtimeContract.GroupNames.Watchlist(watchlistId))
            .SendAsync(MarketDataRealtimeContract.EventNames.WatchlistSnapshot, snapshot, cancellationToken);
    }

    public async Task PublishSubscribedWatchlistsAsync(CancellationToken cancellationToken)
    {
        foreach (var watchlistId in _watchlistConnections.Keys)
        {
            await PublishWatchlistSnapshotAsync(watchlistId, cancellationToken);
        }
    }

    private void ScheduleHomeFlushLocked(Instant now)
    {
        if (_homeFlushCts is not null)
        {
            return;
        }

        // Home refresh hints are the UI's bounded push trigger, so in-window changes must schedule a later flush instead of
        // relying on a future publish to happen after the throttle window.
        var delay = _nextHomeEligibleUtc - now;
        if (delay < Duration.Zero)
        {
            delay = Duration.Zero;
        }

        var cts = new CancellationTokenSource();
        _homeFlushCts = cts;
        _ = RunDeferredHomeFlushAsync(delay, cts.Token);
    }

    private void CancelHomeFlushLocked()
    {
        _homeFlushCts?.Cancel();
        _homeFlushCts?.Dispose();
        _homeFlushCts = null;
    }

    private async Task RunDeferredHomeFlushAsync(Duration delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay.ToTimeSpan(), cancellationToken);

            using var scope = serviceScopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IMarketDataRealtimeOrchestrator>();
            await orchestrator.FlushPendingHomeRefreshHintAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ScheduleWatchlistFlushLocked(Guid watchlistId, WatchlistFlushState flushState, Instant now)
    {
        if (flushState.FlushCts is not null)
        {
            return;
        }

        // Watchlist updates must coalesce rather than disappear when burst traffic stops inside the throttle window.
        var delay = flushState.NextEligibleUtc - now;
        if (delay < Duration.Zero)
        {
            delay = Duration.Zero;
        }

        var cts = new CancellationTokenSource();
        flushState.FlushCts = cts;
        _ = RunDeferredWatchlistFlushAsync(watchlistId, delay, cts.Token);
    }

    private void CancelWatchlistFlushLocked(WatchlistFlushState flushState)
    {
        flushState.FlushCts?.Cancel();
        flushState.FlushCts?.Dispose();
        flushState.FlushCts = null;
    }

    private void CancelWatchlistFlush(WatchlistFlushState flushState)
    {
        lock (flushState)
        {
            CancelWatchlistFlushLocked(flushState);
        }
    }

    private async Task RunDeferredWatchlistFlushAsync(Guid watchlistId, Duration delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay.ToTimeSpan(), cancellationToken);

            using var scope = serviceScopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IMarketDataRealtimeOrchestrator>();
            await orchestrator.FlushPendingWatchlistSnapshotAsync(watchlistId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
