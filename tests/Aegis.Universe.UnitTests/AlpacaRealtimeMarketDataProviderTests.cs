using Alpaca.Markets;
using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Adapters.Alpaca.Services;
using Aegis.Shared.Ports.MarketData;
using NodaTime;
using NSubstitute;
using Shouldly;
using System.Threading.Channels;
using System.Reflection;

namespace Aegis.Universe.UnitTests;

public sealed class AlpacaRealtimeMarketDataProviderTests
{
    [Fact]
    public async Task StartAsync_ShouldConnectEmitStatusAndApplyInitialSubscriptions()
    {
        var timestamp = Instant.FromUtc(2026, 3, 14, 14, 30);
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(timestamp);

        var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        streamingClient.ConnectAndAuthenticateAsync(Arg.Any<CancellationToken>()).Returns(AuthStatus.Authorized);

        var tradeSubscription = new TestDataSubscription<ITrade>("trades", "AAPL");
        var quoteSubscription = new TestDataSubscription<IQuote>("quotes", "MSFT");
        streamingClient.GetTradeSubscription("AAPL").Returns(tradeSubscription);
        streamingClient.GetQuoteSubscription("MSFT").Returns(quoteSubscription);

        streamingClient
            .When(x => x.DisconnectAsync(Arg.Any<CancellationToken>()))
            .Do(_ => streamingClient.SocketClosed += Raise.Event<Action>());

        var marketClock = Substitute.For<global::Alpaca.Markets.IClock>();
        marketClock.IsOpen.Returns(true);
        marketClock.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 30, 0, DateTimeKind.Utc));
        marketClock.NextOpenUtc.Returns(new DateTime(2026, 3, 15, 13, 30, 0, DateTimeKind.Utc));
        marketClock.NextCloseUtc.Returns(new DateTime(2026, 3, 14, 20, 0, 0, DateTimeKind.Utc));

        var tradingClient = Substitute.For<IAlpacaTradingClient>();
        tradingClient.GetClockAsync(Arg.Any<CancellationToken>()).Returns(marketClock);

        await using var provider = CreateProvider(clock, streamingClient, tradingClient);
        await provider.ApplySubscriptionSnapshotAsync(new RealtimeMarketDataSubscriptionSet(
            Trades: ["aapl"],
            Quotes: [" msft "],
            MinuteBars: [],
            UpdatedBars: [],
            DailyBars: [],
            TradingStatuses: [],
            TradeCorrections: [],
            TradeCancels: []), CancellationToken.None);

        await provider.StartAsync(CancellationToken.None);

        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("Connected");
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("AlpacaDataStreamingAuthorized");
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeMarketStatusEvent>().IsOpen.ShouldBeTrue();

        await streamingClient.Received(1).SubscribeAsync(Arg.Is<IEnumerable<IAlpacaDataSubscription>>(subscriptions => subscriptions.Count() == 2), Arg.Any<CancellationToken>());

        var trade = Substitute.For<ITrade>();
        trade.Symbol.Returns("aapl");
        trade.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 30, 1, DateTimeKind.Utc));
        trade.Price.Returns(101.25m);
        trade.Size.Returns(10m);
        trade.TradeId.Returns(7UL);
        trade.Conditions.Returns([]);
        tradeSubscription.Emit(trade);

        var quote = Substitute.For<IQuote>();
        quote.Symbol.Returns("msft");
        quote.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 30, 2, DateTimeKind.Utc));
        quote.BidPrice.Returns(300m);
        quote.AskPrice.Returns(300.1m);
        quote.BidSize.Returns(5m);
        quote.AskSize.Returns(6m);
        quote.Conditions.Returns([]);
        quoteSubscription.Emit(quote);

        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeTradeEvent>().Trade.Symbol.ShouldBe("AAPL");
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeQuoteEvent>().Symbol.ShouldBe("MSFT");

        await provider.StopAsync(CancellationToken.None);

        await streamingClient.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("Disconnected");
    }

    [Fact]
    public async Task ApplySubscriptionSnapshotAsync_WhenConnected_ShouldReplaceRegisteredSubscriptions()
    {
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 3, 14, 14, 30));

        var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        streamingClient.ConnectAndAuthenticateAsync(Arg.Any<CancellationToken>()).Returns(AuthStatus.Authorized);

        var oldTradeSubscription = new TestDataSubscription<ITrade>("trades", "AAPL");
        var newTradeSubscription = new TestDataSubscription<ITrade>("trades", "MSFT");
        streamingClient.GetTradeSubscription("AAPL").Returns(oldTradeSubscription);
        streamingClient.GetTradeSubscription("MSFT").Returns(newTradeSubscription);

        var marketClock = Substitute.For<global::Alpaca.Markets.IClock>();
        marketClock.IsOpen.Returns(true);
        marketClock.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 30, 0, DateTimeKind.Utc));
        marketClock.NextOpenUtc.Returns(new DateTime(2026, 3, 15, 13, 30, 0, DateTimeKind.Utc));
        marketClock.NextCloseUtc.Returns(new DateTime(2026, 3, 14, 20, 0, 0, DateTimeKind.Utc));

        var tradingClient = Substitute.For<IAlpacaTradingClient>();
        tradingClient.GetClockAsync(Arg.Any<CancellationToken>()).Returns(marketClock);

        await using var provider = CreateProvider(clock, streamingClient, tradingClient);
        await provider.ApplySubscriptionSnapshotAsync(CreateSubscriptions(trades: ["AAPL"]), CancellationToken.None);
        await provider.StartAsync(CancellationToken.None);
        await DrainUntilQuietAsync(provider.Events);

        await provider.ApplySubscriptionSnapshotAsync(CreateSubscriptions(trades: ["MSFT"]), CancellationToken.None);

        await streamingClient.Received(1).UnsubscribeAsync(Arg.Is<IEnumerable<IAlpacaDataSubscription>>(subscriptions => subscriptions.Single() == oldTradeSubscription), Arg.Any<CancellationToken>());
        await streamingClient.Received(2).SubscribeAsync(Arg.Any<IEnumerable<IAlpacaDataSubscription>>(), Arg.Any<CancellationToken>());

        var newTrade = Substitute.For<ITrade>();
        newTrade.Symbol.Returns("msft");
        newTrade.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 31, 0, DateTimeKind.Utc));
        newTrade.Price.Returns(300m);
        newTrade.Size.Returns(2m);
        newTrade.TradeId.Returns(99UL);
        newTrade.Conditions.Returns([]);

        oldTradeSubscription.Emit(newTrade);
        await AssertNoEventAsync(provider.Events);

        newTradeSubscription.Emit(newTrade);
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeTradeEvent>().Trade.Symbol.ShouldBe("MSFT");
    }

    [Fact]
    public async Task ApplySubscriptionSnapshotAsync_WhenDesiredStateIsUnchanged_ShouldBeNoOp()
    {
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 3, 14, 14, 30));

        var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        streamingClient.ConnectAndAuthenticateAsync(Arg.Any<CancellationToken>()).Returns(AuthStatus.Authorized);

        var tradeSubscription = new TestDataSubscription<ITrade>("trades", "AAPL");
        streamingClient.GetTradeSubscription("AAPL").Returns(tradeSubscription);

        var tradingClient = Substitute.For<IAlpacaTradingClient>();
        var marketClock = CreateMarketClock();
        tradingClient.GetClockAsync(Arg.Any<CancellationToken>()).Returns(marketClock);

        await using var provider = CreateProvider(clock, streamingClient, tradingClient);
        var subscriptions = CreateSubscriptions(trades: ["AAPL"]);

        await provider.ApplySubscriptionSnapshotAsync(subscriptions, CancellationToken.None);
        await provider.StartAsync(CancellationToken.None);
        await DrainUntilQuietAsync(provider.Events);

        await provider.ApplySubscriptionSnapshotAsync(CreateSubscriptions(trades: [" aapl "]), CancellationToken.None);

        await streamingClient.DidNotReceive().UnsubscribeAsync(Arg.Any<IEnumerable<IAlpacaDataSubscription>>(), Arg.Any<CancellationToken>());
        await streamingClient.Received(1).SubscribeAsync(Arg.Any<IEnumerable<IAlpacaDataSubscription>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplySubscriptionSnapshotAsync_WhenOnlyAddingSymbols_ShouldSubscribeDiffOnly()
    {
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 3, 14, 14, 30));

        var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        streamingClient.ConnectAndAuthenticateAsync(Arg.Any<CancellationToken>()).Returns(AuthStatus.Authorized);

        var aaplSubscription = new TestDataSubscription<ITrade>("trades", "AAPL");
        var msftSubscription = new TestDataSubscription<ITrade>("trades", "MSFT");
        streamingClient.GetTradeSubscription("AAPL").Returns(aaplSubscription);
        streamingClient.GetTradeSubscription("MSFT").Returns(msftSubscription);

        var tradingClient = Substitute.For<IAlpacaTradingClient>();
        var marketClock = CreateMarketClock();
        tradingClient.GetClockAsync(Arg.Any<CancellationToken>()).Returns(marketClock);

        await using var provider = CreateProvider(clock, streamingClient, tradingClient);
        await provider.ApplySubscriptionSnapshotAsync(CreateSubscriptions(trades: ["AAPL"]), CancellationToken.None);
        await provider.StartAsync(CancellationToken.None);
        await DrainUntilQuietAsync(provider.Events);

        await provider.ApplySubscriptionSnapshotAsync(CreateSubscriptions(trades: ["AAPL", "MSFT"]), CancellationToken.None);

        await streamingClient.DidNotReceive().UnsubscribeAsync(Arg.Any<IEnumerable<IAlpacaDataSubscription>>(), Arg.Any<CancellationToken>());
        await streamingClient.Received(2).SubscribeAsync(
            Arg.Any<IEnumerable<IAlpacaDataSubscription>>(),
            Arg.Any<CancellationToken>());
        await streamingClient.Received(1).SubscribeAsync(
            Arg.Is<IEnumerable<IAlpacaDataSubscription>>(subscriptions => subscriptions.Single() == msftSubscription),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplySubscriptionSnapshotAsync_WhenLiveUpdateFails_ShouldEmitNormalizedProviderError()
    {
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 3, 14, 14, 30));

        var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        streamingClient.ConnectAndAuthenticateAsync(Arg.Any<CancellationToken>()).Returns(AuthStatus.Authorized);
        streamingClient.GetTradeSubscription("AAPL").Returns(new TestDataSubscription<ITrade>("trades", "AAPL"));
        streamingClient.GetTradeSubscription("MSFT").Returns(new TestDataSubscription<ITrade>("trades", "MSFT"));
        streamingClient
            .When(x => x.SubscribeAsync(
                Arg.Is<IEnumerable<IAlpacaDataSubscription>>(subscriptions => subscriptions.Any(y => y.Streams.Single() == "trades.MSFT")),
                Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("subscription limit exceeded for feed"));

        var tradingClient = Substitute.For<IAlpacaTradingClient>();
        var marketClock = CreateMarketClock();
        tradingClient.GetClockAsync(Arg.Any<CancellationToken>()).Returns(marketClock);

        await using var provider = CreateProvider(clock, streamingClient, tradingClient);
        await provider.ApplySubscriptionSnapshotAsync(CreateSubscriptions(trades: ["AAPL"]), CancellationToken.None);
        await provider.StartAsync(CancellationToken.None);
        await DrainUntilQuietAsync(provider.Events);

        await provider.ApplySubscriptionSnapshotAsync(CreateSubscriptions(trades: ["AAPL", "MSFT"]), CancellationToken.None);

        var error = await ReadEventAsync(provider.Events);
        error.ShouldBeOfType<RealtimeProviderErrorEvent>().ErrorCode.ShouldBe("subscription_limit_exceeded");
        error.ShouldBeOfType<RealtimeProviderErrorEvent>().IsTransient.ShouldBeFalse();
    }

    [Fact]
    public async Task EventsChannel_WhenFull_ShouldApplyBackpressureToProducer()
    {
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 3, 14, 14, 30));

        var options = CreateOptions(eventBufferCapacity: 1);
        var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        var tradingClient = Substitute.For<IAlpacaTradingClient>();
        await using var provider = CreateProvider(clock, streamingClient, tradingClient, options);

        var firstEvent = new RealtimeProviderStatusEvent("Connected", "stream_connected", true, false, false, clock.GetCurrentInstant(), "alpaca", "iex");
        var secondEvent = new RealtimeProviderStatusEvent("Authenticated", "authentication_completed", true, true, false, clock.GetCurrentInstant(), "alpaca", "iex");

        PublishEvent(provider, firstEvent);
        var blockedEmit = Task.Run(() => PublishEvent(provider, secondEvent));

        await Task.Delay(100);
        blockedEmit.IsCompleted.ShouldBeFalse();

        (await ReadEventAsync(provider.Events)).ShouldBe(firstEvent);
        await blockedEmit.WaitAsync(CancellationToken.None);
        (await ReadEventAsync(provider.Events)).ShouldBe(secondEvent);
    }

    [Fact]
    public async Task StartAsync_WhenConnectionFails_ShouldEmitNormalizedProviderError()
    {
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 3, 14, 14, 30));

        var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        streamingClient.ConnectAndAuthenticateAsync(Arg.Any<CancellationToken>()).Returns<Task<AuthStatus>>(_ => throw new TimeoutException("stream timed out"));

        var tradingClient = Substitute.For<IAlpacaTradingClient>();

        var options = CreateOptions(reconnectInitialDelaySeconds: 30, reconnectMaxDelaySeconds: 30);

        await using var provider = CreateProvider(clock, streamingClient, tradingClient, options);
        await provider.StartAsync(CancellationToken.None);

        var error = await ReadEventAsync(provider.Events);
        error.ShouldBeOfType<RealtimeProviderErrorEvent>().ErrorCode.ShouldBe("timeout");
        error.ShouldBeOfType<RealtimeProviderErrorEvent>().IsTransient.ShouldBeTrue();

        await provider.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenCredentialsAreMissing_ShouldEmitNormalizedConfigurationFailureEvents()
    {
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 3, 14, 14, 30));

        var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        var tradingClient = Substitute.For<IAlpacaTradingClient>();
        var options = new AlpacaRealtimeOptions
        {
            ApiKey = string.Empty,
            ApiSecret = string.Empty,
            Environment = "paper",
            Feed = "iex",
            ConnectTimeoutSeconds = 5,
            EventBufferCapacity = 32,
            ReconnectInitialDelaySeconds = 1,
            ReconnectMaxDelaySeconds = 2
        };

        await using var provider = CreateProvider(clock, streamingClient, tradingClient, options);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => provider.StartAsync(CancellationToken.None));
        exception.Message.ShouldBe("Alpaca realtime credentials are required.");

        var statusEvent = await ReadEventAsync(provider.Events);
        statusEvent.ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("ConfigurationInvalid");
        statusEvent.ShouldBeOfType<RealtimeProviderStatusEvent>().StatusMessage.ShouldBe("configuration_invalid");
        statusEvent.ShouldBeOfType<RealtimeProviderStatusEvent>().IsTerminal.ShouldBeFalse();

        var errorEvent = await ReadEventAsync(provider.Events);
        errorEvent.ShouldBeOfType<RealtimeProviderErrorEvent>().ErrorCode.ShouldBe("invalid_operation");
        errorEvent.ShouldBeOfType<RealtimeProviderErrorEvent>().ErrorMessage.ShouldBe("Alpaca realtime credentials are required.");
        errorEvent.ShouldBeOfType<RealtimeProviderErrorEvent>().IsTransient.ShouldBeFalse();
    }

    [Fact]
    public async Task SocketClosed_ShouldReconnectAndReapplyDesiredSubscriptions()
    {
        var timestamp = Instant.FromUtc(2026, 3, 14, 14, 30);
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(timestamp);

        var firstStreamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        firstStreamingClient.ConnectAndAuthenticateAsync(Arg.Any<CancellationToken>()).Returns(AuthStatus.Authorized);
        var firstTradeSubscription = new TestDataSubscription<ITrade>("trades", "AAPL");
        firstStreamingClient.GetTradeSubscription("AAPL").Returns(firstTradeSubscription);

        var secondStreamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        secondStreamingClient.ConnectAndAuthenticateAsync(Arg.Any<CancellationToken>()).Returns(AuthStatus.Authorized);
        var secondTradeSubscription = new TestDataSubscription<ITrade>("trades", "AAPL");
        secondStreamingClient.GetTradeSubscription("AAPL").Returns(secondTradeSubscription);

        var firstMarketClock = CreateMarketClock();
        var firstTradingClient = Substitute.For<IAlpacaTradingClient>();
        firstTradingClient.GetClockAsync(Arg.Any<CancellationToken>()).Returns(firstMarketClock);

        var secondMarketClock = CreateMarketClock();
        var secondTradingClient = Substitute.For<IAlpacaTradingClient>();
        secondTradingClient.GetClockAsync(Arg.Any<CancellationToken>()).Returns(secondMarketClock);

        var options = CreateOptions(reconnectInitialDelaySeconds: 1, reconnectMaxDelaySeconds: 1);

        await using var provider = new AlpacaRealtimeMarketDataProvider(
            new SequenceClientFactory(
                (firstStreamingClient, firstTradingClient),
                (secondStreamingClient, secondTradingClient)),
            options,
            clock);

        await provider.ApplySubscriptionSnapshotAsync(CreateSubscriptions(trades: ["AAPL"]), CancellationToken.None);
        await provider.StartAsync(CancellationToken.None);

        await DrainEventsAsync(provider.Events, 3);
        await firstStreamingClient.Received(1).SubscribeAsync(
            Arg.Is<IEnumerable<IAlpacaDataSubscription>>(subscriptions => subscriptions.Single() == firstTradeSubscription),
            Arg.Any<CancellationToken>());

        firstStreamingClient.SocketClosed += Raise.Event<Action>();

        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("Disconnected");
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("Connected");
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("AlpacaDataStreamingAuthorized");
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeMarketStatusEvent>().IsOpen.ShouldBeTrue();

        await secondStreamingClient.Received(1).SubscribeAsync(
            Arg.Is<IEnumerable<IAlpacaDataSubscription>>(subscriptions => subscriptions.Single() == secondTradeSubscription),
            Arg.Any<CancellationToken>());

        secondTradeSubscription.Emit(CreateTrade("aapl", 1));
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeTradeEvent>().Trade.Symbol.ShouldBe("AAPL");

        await provider.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_WhenEventChannelIsFull_ShouldNotHang()
    {
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 3, 14, 14, 30));

        var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        var tradingClient = Substitute.For<IAlpacaTradingClient>();

        await using var provider = CreateProvider(clock, streamingClient, tradingClient, CreateOptions(eventBufferCapacity: 1));
        var runCts = new CancellationTokenSource();
        var publishCts = new CancellationTokenSource();
        SetPrivateField(provider, "_runCts", runCts);
        SetPrivateField(provider, "_publishCts", publishCts);
        SetPrivateField(provider, "_runTask", Task.Delay(Timeout.Infinite, runCts.Token));

        PublishEvent(provider, new RealtimeProviderStatusEvent("Filled", "filled", true, true, false, clock.GetCurrentInstant(), "alpaca", "iex"));
        var blockedPublish = Task.Run(() => PublishEvent(provider, new RealtimeProviderStatusEvent("Blocked", "blocked", true, true, false, clock.GetCurrentInstant(), "alpaca", "iex")));

        await Task.Delay(100);
        blockedPublish.IsCompleted.ShouldBeFalse();

        using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await provider.StopAsync(stopCts.Token);
        await blockedPublish.WaitAsync(stopCts.Token);
        blockedPublish.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCapabilitiesAsync_ShouldExposeProviderCapabilityFlags()
    {
        var clock = Substitute.For<NodaTime.IClock>();
        var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
        var tradingClient = Substitute.For<IAlpacaTradingClient>();

        await using var provider = CreateProvider(clock, streamingClient, tradingClient, CreateOptions());

        var capabilities = await provider.GetCapabilitiesAsync(CancellationToken.None);

        capabilities.ProviderName.ShouldBe("alpaca");
        capabilities.DefaultFeed.ShouldBe("iex");
        capabilities.SupportsHistoricalBatches.ShouldBeTrue();
        capabilities.SupportsRevisionEvents.ShouldBeTrue();
        capabilities.SupportsIncrementalSubscriptionChanges.ShouldBeTrue();
        capabilities.SupportsPartialSubscriptionFailures.ShouldBeFalse();
        capabilities.MaxSymbolsPerIncrementalSubscriptionChange.ShouldBeNull();
    }

    private static AlpacaRealtimeMarketDataProvider CreateProvider(
        NodaTime.IClock clock,
        IAlpacaDataStreamingClient streamingClient,
        IAlpacaTradingClient tradingClient,
        AlpacaRealtimeOptions? options = null)
        => new(new TestClientFactory(streamingClient, tradingClient), options ?? CreateOptions(), clock);

    private static AlpacaRealtimeOptions CreateOptions(
        int eventBufferCapacity = 32,
        int reconnectInitialDelaySeconds = 1,
        int reconnectMaxDelaySeconds = 2) => new()
    {
        ApiKey = "key",
        ApiSecret = "secret",
        Environment = "paper",
        Feed = "iex",
        ConnectTimeoutSeconds = 5,
        EventBufferCapacity = eventBufferCapacity,
        ReconnectInitialDelaySeconds = reconnectInitialDelaySeconds,
        ReconnectMaxDelaySeconds = reconnectMaxDelaySeconds
    };

    private static RealtimeMarketDataSubscriptionSet CreateSubscriptions(IReadOnlyCollection<string>? trades = null) => new(
        Trades: trades ?? [],
        Quotes: [],
        MinuteBars: [],
        UpdatedBars: [],
        DailyBars: [],
        TradingStatuses: [],
        TradeCorrections: [],
        TradeCancels: []);

    private static ITrade CreateTrade(string symbol, ulong tradeId)
    {
        var trade = Substitute.For<ITrade>();
        trade.Symbol.Returns(symbol);
        trade.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 30, 0, DateTimeKind.Utc));
        trade.Price.Returns(100m + tradeId);
        trade.Size.Returns(1m);
        trade.TradeId.Returns(tradeId);
        trade.Conditions.Returns([]);
        return trade;
    }

    private static global::Alpaca.Markets.IClock CreateMarketClock()
    {
        var marketClock = Substitute.For<global::Alpaca.Markets.IClock>();
        marketClock.IsOpen.Returns(true);
        marketClock.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 30, 0, DateTimeKind.Utc));
        marketClock.NextOpenUtc.Returns(new DateTime(2026, 3, 15, 13, 30, 0, DateTimeKind.Utc));
        marketClock.NextCloseUtc.Returns(new DateTime(2026, 3, 14, 20, 0, 0, DateTimeKind.Utc));
        return marketClock;
    }

    private static async Task<RealtimeMarketDataEvent> ReadEventAsync(ChannelReader<RealtimeMarketDataEvent> reader)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        return await reader.ReadAsync(cts.Token);
    }

    private static async Task DrainEventsAsync(ChannelReader<RealtimeMarketDataEvent> reader, int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ReadEventAsync(reader);
        }
    }

    private static async Task DrainUntilQuietAsync(ChannelReader<RealtimeMarketDataEvent> reader)
    {
        while (true)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

            try
            {
                await reader.ReadAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static void PublishEvent(AlpacaRealtimeMarketDataProvider provider, RealtimeMarketDataEvent marketDataEvent)
    {
        var method = typeof(AlpacaRealtimeMarketDataProvider).GetMethod("PublishEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();
        method.Invoke(provider, [marketDataEvent]);
    }

    private static void SetPrivateField<TValue>(AlpacaRealtimeMarketDataProvider provider, string fieldName, TValue value)
    {
        var field = typeof(AlpacaRealtimeMarketDataProvider).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.ShouldNotBeNull();
        field.SetValue(provider, value);
    }

    private static async Task AssertNoEventAsync(ChannelReader<RealtimeMarketDataEvent> reader)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Should.ThrowAsync<OperationCanceledException>(async () => await reader.ReadAsync(cts.Token));
    }

    private sealed class TestClientFactory(IAlpacaDataStreamingClient streamingClient, IAlpacaTradingClient tradingClient) : IAlpacaRealtimeClientFactory
    {
        public IAlpacaDataStreamingClient CreateDataStreamingClient(AlpacaRealtimeOptions options) => streamingClient;

        public IAlpacaTradingClient CreateTradingClient(AlpacaRealtimeOptions options) => tradingClient;
    }

    private sealed class SequenceClientFactory(params (IAlpacaDataStreamingClient StreamingClient, IAlpacaTradingClient TradingClient)[] sessions) : IAlpacaRealtimeClientFactory
    {
        private readonly Queue<(IAlpacaDataStreamingClient StreamingClient, IAlpacaTradingClient TradingClient)> _remainingSessions = new(sessions);
        private (IAlpacaDataStreamingClient StreamingClient, IAlpacaTradingClient TradingClient)? _pendingSession;

        public IAlpacaDataStreamingClient CreateDataStreamingClient(AlpacaRealtimeOptions options)
        {
            _pendingSession = _remainingSessions.Dequeue();
            return _pendingSession.Value.StreamingClient;
        }

        public IAlpacaTradingClient CreateTradingClient(AlpacaRealtimeOptions options)
        {
            _pendingSession.ShouldNotBeNull();
            var tradingClient = _pendingSession.Value.TradingClient;
            _pendingSession = null;
            return tradingClient;
        }
    }

    private sealed class TestDataSubscription<T>(string stream, string symbol) : IAlpacaDataSubscription<T>
    {
        public event Action<T>? Received;

        public event Action? OnSubscribedChanged;

        public IEnumerable<string> Streams { get; } = [$"{stream}.{symbol}"];

        public bool Subscribed { get; private set; }

        public void Emit(T value) => Received?.Invoke(value);

        public void SetSubscribed(bool subscribed)
        {
            Subscribed = subscribed;
            OnSubscribedChanged?.Invoke();
        }
    }
}
