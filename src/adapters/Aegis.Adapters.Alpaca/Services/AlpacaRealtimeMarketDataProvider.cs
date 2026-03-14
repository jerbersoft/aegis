using System.Threading.Channels;
using Alpaca.Markets;
using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Shared.Ports.MarketData;
using NodaTime;

namespace Aegis.Adapters.Alpaca.Services;

public sealed class AlpacaRealtimeMarketDataProvider(
    IAlpacaRealtimeClientFactory clientFactory,
    AlpacaRealtimeOptions options,
    NodaTime.IClock clock) : IRealtimeMarketDataProvider
{
    private sealed class SubscriptionRegistration(string streamKey, IAlpacaDataSubscription subscription, Action detach)
    {
        public string StreamKey { get; } = streamKey;

        public IAlpacaDataSubscription Subscription { get; } = subscription;

        public Action Detach { get; } = detach;
    }

    private sealed class ClientSession(
        IAlpacaDataStreamingClient streamingClient,
        IAlpacaTradingClient tradingClient,
        TaskCompletionSource<Exception?> connectionEnded)
    {
        public IAlpacaDataStreamingClient StreamingClient { get; } = streamingClient;

        public IAlpacaTradingClient TradingClient { get; } = tradingClient;

        public TaskCompletionSource<Exception?> ConnectionEnded { get; } = connectionEnded;

        public Dictionary<string, SubscriptionRegistration> Registrations { get; } = new(StringComparer.Ordinal);
    }

    private sealed record SubscriptionDefinition(string StreamKey, Func<SubscriptionRegistration> CreateRegistration);

    private readonly Channel<RealtimeMarketDataEvent> _events = Channel.CreateBounded<RealtimeMarketDataEvent>(new BoundedChannelOptions(Math.Max(1, options.EventBufferCapacity))
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = false,
        SingleWriter = false
    });
    private readonly SemaphoreSlim _lifecycleSync = new(1, 1);
    private readonly SemaphoreSlim _subscriptionSync = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();

    private RealtimeMarketDataSubscriptionSet _desiredSubscriptions = RealtimeMarketDataSubscriptionSet.Empty;
    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _publishCts;
    private Task? _runTask;
    private ClientSession? _activeSession;
    private bool _disposed;

    public ChannelReader<RealtimeMarketDataEvent> Events => _events.Reader;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiSecret))
        {
            var exception = new InvalidOperationException("Alpaca realtime credentials are required.");
            PublishConfigurationFailure(exception);
            throw exception;
        }

        await _lifecycleSync.WaitAsync(cancellationToken);
        try
        {
            if (_runTask is not null)
            {
                return;
            }

            _runCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
            _publishCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
            _runTask = RunAsync(_runCts.Token);
        }
        finally
        {
            _lifecycleSync.Release();
        }
    }

    public async Task ApplySubscriptionSnapshotAsync(RealtimeMarketDataSubscriptionSet subscriptionSet, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var normalized = NormalizeSubscriptionSet(subscriptionSet);
        _desiredSubscriptions = normalized;

        var session = _activeSession;
        if (session is null)
        {
            return;
        }

        try
        {
            await ReplaceSubscriptionsAsync(session, normalized, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            PublishEvent(AlpacaRealtimeContractMapper.MapProviderError(exception, GetProviderFeedOrNull(), clock.GetCurrentInstant()));
        }
    }

    public ValueTask<RealtimeMarketDataProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(AlpacaRealtimeContractResolver.BuildCapabilities(options));

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task? runTask;
        CancellationTokenSource? runCts;
        CancellationTokenSource? publishCts;

        await _lifecycleSync.WaitAsync(cancellationToken);
        try
        {
            runTask = _runTask;
            runCts = _runCts;
            publishCts = _publishCts;
        }
        finally
        {
            _lifecycleSync.Release();
        }

        if (runTask is null)
        {
            return;
        }

        runCts?.Cancel();
        publishCts?.Cancel();

        try
        {
            await runTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (runCts?.IsCancellationRequested == true)
        {
        }
        finally
        {
            await _lifecycleSync.WaitAsync(cancellationToken);
            try
            {
                if (ReferenceEquals(_runTask, runTask))
                {
                    _runTask = null;
                }

                if (ReferenceEquals(_runCts, runCts))
                {
                    _runCts = null;
                }

                if (ReferenceEquals(_publishCts, publishCts))
                {
                    _publishCts = null;
                }
            }
            finally
            {
                _lifecycleSync.Release();
            }

            publishCts?.Dispose();
            runCts?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposeCts.Cancel();

        try
        {
            await StopAsync(CancellationToken.None);
        }
        finally
        {
            _events.Writer.TryComplete();
            _disposeCts.Dispose();
            _lifecycleSync.Dispose();
            _subscriptionSync.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var nextDelay = Math.Max(1, options.ReconnectInitialDelaySeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            ClientSession? session = null;

            try
            {
                session = CreateSession();
                _activeSession = session;

                await ConnectAsync(session, cancellationToken);
                await PublishMarketStatusAsync(session, cancellationToken);
                await ReplaceSubscriptionsAsync(session, _desiredSubscriptions, cancellationToken);

                nextDelay = Math.Max(1, options.ReconnectInitialDelaySeconds);
                var failure = await session.ConnectionEnded.Task.WaitAsync(cancellationToken);
                if (failure is not null)
                {
                    PublishEvent(AlpacaRealtimeContractMapper.MapProviderError(failure, GetProviderFeedOrNull(), clock.GetCurrentInstant()));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                if (IsConfigurationException(exception))
                {
                    PublishConfigurationFailure(exception);
                    break;
                }

                PublishEvent(AlpacaRealtimeContractMapper.MapProviderError(exception, GetProviderFeedOrNull(), clock.GetCurrentInstant()));
            }
            finally
            {
                await CleanupSessionAsync(session, cancellationToken);
                if (ReferenceEquals(_activeSession, session))
                {
                    _activeSession = null;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(nextDelay), cancellationToken);
            nextDelay = Math.Min(Math.Max(nextDelay * 2, 1), Math.Max(nextDelay, options.ReconnectMaxDelaySeconds));
        }
    }

    private ClientSession CreateSession()
    {
        var streamingClient = clientFactory.CreateDataStreamingClient(options);
        var tradingClient = clientFactory.CreateTradingClient(options);
        var connectionEnded = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);

        streamingClient.SocketClosed += () =>
        {
            PublishEvent(AlpacaRealtimeContractMapper.MapProviderStatus("Disconnected", GetProviderFeedOrNull(), clock.GetCurrentInstant()));
            connectionEnded.TrySetResult(null);
        };
        streamingClient.OnError += exception => connectionEnded.TrySetResult(exception);

        return new ClientSession(streamingClient, tradingClient, connectionEnded);
    }

    private async Task ConnectAsync(ClientSession session, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.ConnectTimeoutSeconds)));

        var authStatus = await session.StreamingClient.ConnectAndAuthenticateAsync(timeoutCts.Token);
        PublishEvent(AlpacaRealtimeContractMapper.MapProviderStatus("Connected", GetProviderFeedOrNull(), clock.GetCurrentInstant()));
        PublishEvent(AlpacaRealtimeContractMapper.MapProviderStatus(MapAuthStatus(authStatus), GetProviderFeedOrNull(), clock.GetCurrentInstant()));

        if (authStatus != AuthStatus.Authorized)
        {
            throw new InvalidOperationException($"Alpaca realtime authentication failed with status '{authStatus}'.");
        }
    }

    private async Task PublishMarketStatusAsync(ClientSession session, CancellationToken cancellationToken)
    {
        try
        {
            var marketClock = await session.TradingClient.GetClockAsync(cancellationToken);
            PublishEvent(AlpacaRealtimeContractMapper.MapMarketStatus(marketClock, GetProviderFeedOrNull(), clock.GetCurrentInstant()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            PublishEvent(AlpacaRealtimeContractMapper.MapProviderError(exception, GetProviderFeedOrNull(), clock.GetCurrentInstant()));
        }
    }

    private async Task ReplaceSubscriptionsAsync(
        ClientSession session,
        RealtimeMarketDataSubscriptionSet subscriptionSet,
        CancellationToken cancellationToken)
    {
        await _subscriptionSync.WaitAsync(cancellationToken);
        try
        {
            if (!ReferenceEquals(_activeSession, session))
            {
                return;
            }

            var desiredDefinitions = CreateSubscriptionDefinitions(session.StreamingClient, subscriptionSet);
            var toRemove = session.Registrations
                .Where(x => !desiredDefinitions.ContainsKey(x.Key))
                .Select(x => x.Value)
                .ToArray();

            if (toRemove.Length > 0)
            {
                await session.StreamingClient.UnsubscribeAsync(toRemove.Select(x => x.Subscription), cancellationToken);
                foreach (var registration in toRemove)
                {
                    registration.Detach();
                    session.Registrations.Remove(registration.StreamKey);
                }
            }

            var toAdd = desiredDefinitions
                .Where(x => !session.Registrations.ContainsKey(x.Key))
                .Select(x => x.Value.CreateRegistration())
                .ToArray();

            if (toAdd.Length == 0)
            {
                return;
            }

            await session.StreamingClient.SubscribeAsync(toAdd.Select(x => x.Subscription), cancellationToken);
            foreach (var registration in toAdd)
            {
                session.Registrations[registration.StreamKey] = registration;
            }
        }
        finally
        {
            _subscriptionSync.Release();
        }
    }

    private Dictionary<string, SubscriptionDefinition> CreateSubscriptionDefinitions(
        IAlpacaDataStreamingClient streamingClient,
        RealtimeMarketDataSubscriptionSet subscriptionSet)
    {
        var definitions = new Dictionary<string, SubscriptionDefinition>(StringComparer.Ordinal);

        AddDefinitions(definitions, "trades", subscriptionSet.Trades, symbol => new SubscriptionDefinition(
            BuildStreamKey("trades", symbol),
            () => CreateRegistration(
                BuildStreamKey("trades", symbol),
                streamingClient.GetTradeSubscription(symbol),
                trade => AlpacaRealtimeContractMapper.MapTrade(trade, GetProviderFeedOrNull(), clock.GetCurrentInstant()))));
        AddDefinitions(definitions, "quotes", subscriptionSet.Quotes, symbol => new SubscriptionDefinition(
            BuildStreamKey("quotes", symbol),
            () => CreateRegistration(
                BuildStreamKey("quotes", symbol),
                streamingClient.GetQuoteSubscription(symbol),
                quote => AlpacaRealtimeContractMapper.MapQuote(quote, GetProviderFeedOrNull(), clock.GetCurrentInstant()))));
        AddDefinitions(definitions, "bars", subscriptionSet.MinuteBars, symbol => new SubscriptionDefinition(
            BuildStreamKey("bars", symbol),
            () => CreateRegistration(
                BuildStreamKey("bars", symbol),
                streamingClient.GetMinuteBarSubscription(symbol),
                bar => AlpacaRealtimeContractMapper.MapMinuteBar(bar, GetProviderFeedOrNull(), clock.GetCurrentInstant()))));
        AddDefinitions(definitions, "updatedBars", subscriptionSet.UpdatedBars, symbol => new SubscriptionDefinition(
            BuildStreamKey("updatedBars", symbol),
            () => CreateRegistration(
                BuildStreamKey("updatedBars", symbol),
                streamingClient.GetUpdatedBarSubscription(symbol),
                bar => AlpacaRealtimeContractMapper.MapUpdatedBar(bar, GetProviderFeedOrNull(), clock.GetCurrentInstant()))));
        AddDefinitions(definitions, "dailyBars", subscriptionSet.DailyBars, symbol => new SubscriptionDefinition(
            BuildStreamKey("dailyBars", symbol),
            () => CreateRegistration(
                BuildStreamKey("dailyBars", symbol),
                streamingClient.GetDailyBarSubscription(symbol),
                bar => AlpacaRealtimeContractMapper.MapDailyBar(bar, GetProviderFeedOrNull(), clock.GetCurrentInstant()))));
        AddDefinitions(definitions, "statuses", subscriptionSet.TradingStatuses, symbol => new SubscriptionDefinition(
            BuildStreamKey("statuses", symbol),
            () => CreateRegistration(
                BuildStreamKey("statuses", symbol),
                streamingClient.GetStatusSubscription(symbol),
                status => AlpacaRealtimeContractMapper.MapTradingStatus(status, GetProviderFeedOrNull(), clock.GetCurrentInstant()))));
        AddDefinitions(definitions, "corrections", subscriptionSet.TradeCorrections, symbol => new SubscriptionDefinition(
            BuildStreamKey("corrections", symbol),
            () => CreateRegistration(
                BuildStreamKey("corrections", symbol),
                streamingClient.GetCorrectionSubscription(symbol),
                correction => AlpacaRealtimeContractMapper.MapTradeCorrection(correction, GetProviderFeedOrNull(), clock.GetCurrentInstant()))));
        AddDefinitions(definitions, "cancels", subscriptionSet.TradeCancels, symbol => new SubscriptionDefinition(
            BuildStreamKey("cancels", symbol),
            () => CreateRegistration(
                BuildStreamKey("cancels", symbol),
                streamingClient.GetCancellationSubscription(symbol),
                trade => AlpacaRealtimeContractMapper.MapTradeCancel(trade, GetProviderFeedOrNull(), clock.GetCurrentInstant()))));

        return definitions;
    }

    private static void AddDefinitions(
        Dictionary<string, SubscriptionDefinition> definitions,
        string stream,
        IEnumerable<string> symbols,
        Func<string, SubscriptionDefinition> createDefinition)
    {
        foreach (var symbol in symbols)
        {
            definitions[BuildStreamKey(stream, symbol)] = createDefinition(symbol);
        }
    }

    private SubscriptionRegistration CreateRegistration<TApi>(
        string streamKey,
        IAlpacaDataSubscription<TApi> subscription,
        Func<TApi, RealtimeMarketDataEvent> map)
    {
        // SDK callbacks are synchronous, so the bounded write happens inline to preserve backpressure instead of
        // spawning unbounded fire-and-forget work when the consumer falls behind.
        void Handler(TApi api) => PublishEvent(map(api));

        subscription.Received += Handler;
        return new SubscriptionRegistration(streamKey, subscription, () => subscription.Received -= Handler);
    }

    private async Task CleanupSessionAsync(ClientSession? session, CancellationToken cancellationToken)
    {
        if (session is null)
        {
            return;
        }

        try
        {
            var registrations = session.Registrations.Values.ToArray();
            if (registrations.Length > 0)
            {
                try
                {
                    await session.StreamingClient.UnsubscribeAsync(registrations.Select(x => x.Subscription), CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    foreach (var registration in registrations)
                    {
                        registration.Detach();
                    }

                    session.Registrations.Clear();
                }
            }

            try
            {
                await session.StreamingClient.DisconnectAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }
        }
        finally
        {
            session.StreamingClient.Dispose();
            session.TradingClient.Dispose();
        }
    }

    private void PublishEvent(RealtimeMarketDataEvent marketDataEvent)
    {
        var publishCts = _publishCts;
        var publishToken = publishCts?.Token ?? _disposeCts.Token;

        try
        {
            // StopAsync cancels the active publish token before waiting for shutdown so bounded-channel writers cannot stall teardown.
            _events.Writer.WriteAsync(marketDataEvent, publishToken).AsTask().GetAwaiter().GetResult();
        }
        catch (ChannelClosedException)
        {
        }
        catch (OperationCanceledException) when (publishToken.IsCancellationRequested || _disposeCts.IsCancellationRequested)
        {
        }
    }

    private static string MapAuthStatus(AuthStatus authStatus) => authStatus switch
    {
        AuthStatus.Authorized => "AlpacaDataStreamingAuthorized",
        AuthStatus.Unauthorized => "AlpacaDataStreamingUnauthorized",
        AuthStatus.TooManyConnections => "Failed",
        _ => "Failed"
    };

    private static RealtimeMarketDataSubscriptionSet NormalizeSubscriptionSet(RealtimeMarketDataSubscriptionSet subscriptionSet) => new(
        NormalizeSymbols(subscriptionSet.Trades),
        NormalizeSymbols(subscriptionSet.Quotes),
        NormalizeSymbols(subscriptionSet.MinuteBars),
        NormalizeSymbols(subscriptionSet.UpdatedBars),
        NormalizeSymbols(subscriptionSet.DailyBars),
        NormalizeSymbols(subscriptionSet.TradingStatuses),
        NormalizeSymbols(subscriptionSet.TradeCorrections),
        NormalizeSymbols(subscriptionSet.TradeCancels));

    private static string[] NormalizeSymbols(IReadOnlyCollection<string> symbols) => symbols
        .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
        .Select(symbol => symbol.Trim().ToUpperInvariant())
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static string BuildStreamKey(string stream, string symbol) => $"{stream}.{symbol}";

    private void PublishConfigurationFailure(Exception exception)
    {
        var receivedUtc = clock.GetCurrentInstant();
        var providerFeed = GetProviderFeedOrNull();
        PublishEvent(AlpacaRealtimeContractMapper.MapProviderStatus("ConfigurationInvalid", providerFeed, receivedUtc));
        PublishEvent(AlpacaRealtimeContractMapper.MapProviderError(exception, providerFeed, receivedUtc));
    }

    private string? GetProviderFeedOrNull()
    {
        try
        {
            return AlpacaRealtimeContractResolver.NormalizeFeed(options.Feed);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool IsConfigurationException(Exception exception) =>
        exception is InvalidOperationException &&
        (exception.Message.Contains("credentials are required", StringComparison.OrdinalIgnoreCase)
         || exception.Message.Contains("unsupported alpaca", StringComparison.OrdinalIgnoreCase)
         || exception.Message.Contains("unsupported alpaca streaming endpoint", StringComparison.OrdinalIgnoreCase));

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
