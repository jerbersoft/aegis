using Aegis.Backend.MarketData;
using Aegis.Shared.Contracts.MarketData;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
using Shouldly;

namespace Aegis.MarketData.UnitTests;

public sealed class MarketDataRealtimeOrchestratorTests
{
    [Fact]
    public async Task PublishHomeRefreshHintAsync_ShouldCoalesceScopes_WithinThrottleWindow()
    {
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        var clock = new AdjustableClock(Instant.FromUtc(2026, 3, 14, 14, 0));
        var serviceProvider = BuildServiceProvider(clients, groups, clock, homeThrottleMilliseconds: 1000, watchlistThrottleMilliseconds: 500);
        var orchestrator = serviceProvider.GetRequiredService<IMarketDataRealtimeOrchestrator>();

        await orchestrator.SubscribeHomeAsync("conn-1", CancellationToken.None);

        await orchestrator.PublishHomeRefreshHintAsync([MarketDataRealtimeContract.ChangeScopes.BootstrapStatus], CancellationToken.None);
        await orchestrator.PublishHomeRefreshHintAsync([MarketDataRealtimeContract.ChangeScopes.DailyReadiness], CancellationToken.None);

        clients.GroupMessages.Count.ShouldBe(1);
        var initialMessage = clients.GroupMessages.Single();
        initialMessage.Method.ShouldBe(MarketDataRealtimeContract.EventNames.HomeRefreshHint);
        var initialPayload = initialMessage.Arguments.ShouldHaveSingleItem().ShouldBeOfType<MarketDataHomeRefreshEvent>();
        initialPayload.ChangedScopes.ShouldBe([MarketDataRealtimeContract.ChangeScopes.BootstrapStatus]);

        clock.Advance(Duration.FromMilliseconds(1000));
        await orchestrator.FlushPendingHomeRefreshHintAsync(CancellationToken.None);

        clients.GroupMessages.Count.ShouldBe(2);
        var coalescedMessage = clients.GroupMessages[^1];
        coalescedMessage.Method.ShouldBe(MarketDataRealtimeContract.EventNames.HomeRefreshHint);
        var coalescedPayload = coalescedMessage.Arguments.ShouldHaveSingleItem().ShouldBeOfType<MarketDataHomeRefreshEvent>();
        coalescedPayload.ChangedScopes.ShouldBe([MarketDataRealtimeContract.ChangeScopes.DailyReadiness]);
    }

    [Fact]
    public async Task PublishHomeRefreshHintAsync_ShouldEmitDeferredCoalescedRefresh_WhenThrottleWindowExpiresWithoutAnotherPublish()
    {
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        var clock = new AdjustableClock(Instant.FromUtc(2026, 3, 14, 14, 0));
        var serviceProvider = BuildServiceProvider(clients, groups, clock, homeThrottleMilliseconds: 20, watchlistThrottleMilliseconds: 500);
        var orchestrator = serviceProvider.GetRequiredService<IMarketDataRealtimeOrchestrator>();

        await orchestrator.SubscribeHomeAsync("conn-1", CancellationToken.None);

        await orchestrator.PublishHomeRefreshHintAsync([MarketDataRealtimeContract.ChangeScopes.BootstrapStatus], CancellationToken.None);
        clock.Advance(Duration.FromMilliseconds(5));
        await orchestrator.PublishHomeRefreshHintAsync([MarketDataRealtimeContract.ChangeScopes.DailyReadiness], CancellationToken.None);

        clock.Advance(Duration.FromMilliseconds(20));
        await Task.Delay(60);

        clients.GroupMessages.Count.ShouldBe(2);
        var deferredPayload = clients.GroupMessages[^1].Arguments.ShouldHaveSingleItem().ShouldBeOfType<MarketDataHomeRefreshEvent>();
        deferredPayload.ChangedScopes.ShouldBe([MarketDataRealtimeContract.ChangeScopes.DailyReadiness]);
    }

    [Fact]
    public void MarketDataRealtimeContracts_ShouldSerializeSnakeCaseWireFields()
    {
        var payload = new MarketDataWatchlistSnapshotEvent(
            MarketDataRealtimeContract.ContractVersion,
            "event-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            7,
            Instant.FromUtc(2026, 3, 14, 14, 0),
            Instant.FromUtc(2026, 3, 14, 14, 1),
            true,
            [new MarketDataWatchlistSymbolSnapshot("AAPL", 101.25m, 1.5m)]);

        var json = System.Text.Json.JsonSerializer.Serialize(payload, Aegis.Shared.Serialization.AegisJson.CreateSerializerOptions());

        json.ShouldContain("\"contract_version\"");
        json.ShouldContain("\"event_id\"");
        json.ShouldContain("\"watchlist_id\"");
        json.ShouldContain("\"batch_sequence\"");
        json.ShouldContain("\"occurred_utc\"");
        json.ShouldContain("\"as_of_utc\"");
        json.ShouldContain("\"requires_refresh\"");
        json.ShouldContain("\"current_price\"");
        json.ShouldContain("\"percent_change\"");
        json.ShouldNotContain("\"contractVersion\"");
        json.ShouldNotContain("\"watchlistId\"");
        json.ShouldNotContain("\"batchSequence\"");
        json.ShouldNotContain("\"currentPrice\"");
        json.ShouldNotContain("\"percentChange\"");
    }

    [Fact]
    public async Task PublishWatchlistSnapshotAsync_ShouldRespectThrottleWindow()
    {
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        var builder = new StubWatchlistSnapshotBuilder();
        var watchlistId = Guid.NewGuid();
        builder.SetSnapshot(watchlistId, sequence => new MarketDataWatchlistSnapshotEvent(
            MarketDataRealtimeContract.ContractVersion,
            $"event-{sequence}",
            watchlistId,
            sequence,
            Instant.FromUtc(2026, 3, 14, 14, 0),
            Instant.FromUtc(2026, 3, 14, 14, 0),
            true,
            [new MarketDataWatchlistSymbolSnapshot("AAPL", 101.25m, 1.5m)]));

        var clock = new AdjustableClock(Instant.FromUtc(2026, 3, 14, 14, 0));
        var serviceProvider = BuildServiceProvider(clients, groups, clock, builder, homeThrottleMilliseconds: 1000, watchlistThrottleMilliseconds: 1000);
        var orchestrator = serviceProvider.GetRequiredService<IMarketDataRealtimeOrchestrator>();

        var ack = await orchestrator.SubscribeWatchlistAsync("conn-1", watchlistId, CancellationToken.None);
        ack.ShouldNotBeNull();

        clients.ClientMessages.Count.ShouldBe(1);

        await orchestrator.PublishWatchlistSnapshotAsync(watchlistId, CancellationToken.None);
        await orchestrator.PublishWatchlistSnapshotAsync(watchlistId, CancellationToken.None);

        clients.GroupMessages.Count.ShouldBe(1);
        var payload = clients.GroupMessages.Single().Arguments.ShouldHaveSingleItem().ShouldBeOfType<MarketDataWatchlistSnapshotEvent>();
        payload.BatchSequence.ShouldBe(2);
    }

    [Fact]
    public async Task PublishWatchlistSnapshotAsync_ShouldEmitDeferredCoalescedSnapshot_WhenThrottleWindowExpiresWithoutAnotherPublish()
    {
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        var builder = new StubWatchlistSnapshotBuilder();
        var watchlistId = Guid.NewGuid();
        builder.SetSnapshot(watchlistId, sequence => new MarketDataWatchlistSnapshotEvent(
            MarketDataRealtimeContract.ContractVersion,
            $"event-{sequence}",
            watchlistId,
            sequence,
            Instant.FromUtc(2026, 3, 14, 14, 0),
            Instant.FromUtc(2026, 3, 14, 14, 0),
            true,
            [new MarketDataWatchlistSymbolSnapshot("AAPL", 101.25m + sequence, 1.5m)]));

        var clock = new AdjustableClock(Instant.FromUtc(2026, 3, 14, 14, 0));
        var serviceProvider = BuildServiceProvider(clients, groups, clock, builder, homeThrottleMilliseconds: 1000, watchlistThrottleMilliseconds: 20);
        var orchestrator = serviceProvider.GetRequiredService<IMarketDataRealtimeOrchestrator>();

        var ack = await orchestrator.SubscribeWatchlistAsync("conn-1", watchlistId, CancellationToken.None);
        ack.ShouldNotBeNull();

        clients.ClientMessages.Count.ShouldBe(1);

        await orchestrator.PublishWatchlistSnapshotAsync(watchlistId, CancellationToken.None);
        clock.Advance(Duration.FromMilliseconds(5));
        await orchestrator.PublishWatchlistSnapshotAsync(watchlistId, CancellationToken.None);

        clients.GroupMessages.Count.ShouldBe(1);
        clients.GroupMessages[0].Arguments.ShouldHaveSingleItem().ShouldBeOfType<MarketDataWatchlistSnapshotEvent>().BatchSequence.ShouldBe(2);

        clock.Advance(Duration.FromMilliseconds(20));
        await Task.Delay(60);

        clients.GroupMessages.Count.ShouldBe(2);
        var deferredPayload = clients.GroupMessages[^1].Arguments.ShouldHaveSingleItem().ShouldBeOfType<MarketDataWatchlistSnapshotEvent>();
        deferredPayload.BatchSequence.ShouldBe(3);
    }

    private static ServiceProvider BuildServiceProvider(
        RecordingHubClients clients,
        RecordingGroupManager groups,
        AdjustableClock clock,
        StubWatchlistSnapshotBuilder? snapshotBuilder = null,
        int homeThrottleMilliseconds = 1000,
        int watchlistThrottleMilliseconds = 500)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHubContext<MarketDataHub>>(new TestHubContext(clients, groups));
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IMarketDataWatchlistSnapshotBuilder>(snapshotBuilder ?? new StubWatchlistSnapshotBuilder());
        services.AddSingleton(Options.Create(new MarketDataRealtimeOptions
        {
            HomeRefreshThrottleMilliseconds = homeThrottleMilliseconds,
            WatchlistSnapshotThrottleMilliseconds = watchlistThrottleMilliseconds
        }));
        services.AddSingleton<IMarketDataRealtimeOrchestrator, MarketDataRealtimeOrchestrator>();
        return services.BuildServiceProvider();
    }

    private sealed class AdjustableClock(Instant now) : IClock
    {
        private Instant _now = now;

        public Instant GetCurrentInstant() => _now;

        public void Advance(Duration duration) => _now += duration;
    }

    private sealed class StubWatchlistSnapshotBuilder : IMarketDataWatchlistSnapshotBuilder
    {
        private readonly Dictionary<Guid, Func<long, MarketDataWatchlistSnapshotEvent>> _factories = [];

        public void SetSnapshot(Guid watchlistId, Func<long, MarketDataWatchlistSnapshotEvent> factory) => _factories[watchlistId] = factory;

        public Task<MarketDataWatchlistSnapshotEvent?> BuildAsync(Guid watchlistId, long batchSequence, CancellationToken cancellationToken)
        {
            _factories.TryGetValue(watchlistId, out var factory);
            return Task.FromResult(factory?.Invoke(batchSequence));
        }
    }

    private sealed class TestHubContext(RecordingHubClients clients, RecordingGroupManager groups) : IHubContext<MarketDataHub>
    {
        public IHubClients Clients => clients;

        public IGroupManager Groups => groups;
    }

    private sealed class RecordingHubClients : IHubClients
    {
        public List<RecordedMessage> GroupMessages { get; } = [];
        public List<RecordedMessage> ClientMessages { get; } = [];

        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => new RecordingClientProxy(connectionId, ClientMessages);
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => new RecordingClientProxy(groupName, GroupMessages);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingClientProxy(string target, List<RecordedMessage> sink) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            sink.Add(new RecordedMessage(target, method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed record RecordedMessage(string Target, string Method, object?[] Arguments);
}
