using System.Threading.Channels;
using Alpaca.Markets;
using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Adapters.Alpaca.Services;
using Aegis.Backend.MarketData;
using Aegis.Shared.Ports.MarketData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NodaTime;
using NSubstitute;
using Shouldly;

namespace Aegis.MarketData.IntegrationTests;

public sealed class MarketDataRealtimeProviderHostedIntegrationTests
{
    [Fact]
    public async Task HostLifecycle_ShouldStartAndStopRealtimeProvider_WhenRuntimeEnabled()
    {
        var probe = RealtimeClientProbe.Create(AuthStatus.Authorized);
        using var host = CreateHost(enableProviderRuntime: true, probe, CreateRealtimeOptions(apiKey: "configured-key", apiSecret: "configured-secret"));

        await host.StartAsync();

        var provider = host.Services.GetRequiredService<IRealtimeMarketDataProvider>();
        provider.ShouldBeOfType<AlpacaRealtimeMarketDataProvider>();
        await WaitForAsync(() => probe.ConnectCalls == 1);

        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("Connected");
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("AlpacaDataStreamingAuthorized");
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeMarketStatusEvent>().IsOpen.ShouldBeTrue();

        await host.StopAsync();

        probe.DisconnectCalls.ShouldBe(1);
    }

    [Fact]
    public async Task HostLifecycle_ShouldNotStartRealtimeProvider_WhenRuntimeDisabled()
    {
        var probe = RealtimeClientProbe.Create(AuthStatus.Authorized);
        using var host = CreateHost(enableProviderRuntime: false, probe, CreateRealtimeOptions(apiKey: "configured-key", apiSecret: "configured-secret"));

        await host.StartAsync();

        host.Services.GetRequiredService<IRealtimeMarketDataProvider>().ShouldBeOfType<AlpacaRealtimeMarketDataProvider>();
        probe.ConnectCalls.ShouldBe(0);

        await host.StopAsync();

        probe.DisconnectCalls.ShouldBe(0);
    }

    [Fact]
    public async Task HostLifecycle_ShouldSurfaceNormalizedFailureEvents_WhenAuthenticationFails()
    {
        var probe = RealtimeClientProbe.Create(AuthStatus.Unauthorized);
        using var host = CreateHost(enableProviderRuntime: true, probe, CreateRealtimeOptions(apiKey: "configured-key", apiSecret: "configured-secret"));

        await host.StartAsync();

        var provider = host.Services.GetRequiredService<IRealtimeMarketDataProvider>();

        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("Connected");
        (await ReadEventAsync(provider.Events)).ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("AlpacaDataStreamingUnauthorized");

        var error = await ReadEventAsync(provider.Events);
        error.ShouldBeOfType<RealtimeProviderErrorEvent>().ErrorCode.ShouldBe("invalid_operation");
        error.ShouldBeOfType<RealtimeProviderErrorEvent>().IsTransient.ShouldBeFalse();

        await host.StopAsync();
    }

    [Fact]
    public async Task HostLifecycle_ShouldSurfaceNormalizedFailureEvents_WhenRealtimeConfigurationIsMissing()
    {
        var probe = RealtimeClientProbe.Create(AuthStatus.Authorized);
        using var host = CreateHost(enableProviderRuntime: true, probe, CreateRealtimeOptions(apiKey: string.Empty, apiSecret: string.Empty));

        await host.StartAsync();

        var provider = host.Services.GetRequiredService<IRealtimeMarketDataProvider>();
        probe.ConnectCalls.ShouldBe(0);

        var statusEvent = await ReadEventAsync(provider.Events);
        statusEvent.ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("ConfigurationInvalid");
        statusEvent.ShouldBeOfType<RealtimeProviderStatusEvent>().StatusMessage.ShouldBe("configuration_invalid");

        var errorEvent = await ReadEventAsync(provider.Events);
        errorEvent.ShouldBeOfType<RealtimeProviderErrorEvent>().ErrorCode.ShouldBe("invalid_operation");
        errorEvent.ShouldBeOfType<RealtimeProviderErrorEvent>().ErrorMessage.ShouldBe("Alpaca realtime credentials are required.");
        errorEvent.ShouldBeOfType<RealtimeProviderErrorEvent>().IsTransient.ShouldBeFalse();

        await host.StopAsync();
    }

    private static IHost CreateHost(bool enableProviderRuntime, RealtimeClientProbe probe, AlpacaRealtimeOptions realtimeOptions)
    {
        var clock = Substitute.For<NodaTime.IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 3, 14, 14, 30));

        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(clock);
                services.Configure<MarketDataRealtimeOptions>(options => options.EnableProviderRuntime = enableProviderRuntime);
                services.AddMarketDataRealtimeProviderRuntime(realtimeOptions);
                services.RemoveAll<IAlpacaRealtimeClientFactory>();
                services.AddSingleton(probe);
                services.AddSingleton<IAlpacaRealtimeClientFactory>(probe);
            })
            .Build();
    }

    private static AlpacaRealtimeOptions CreateRealtimeOptions(string apiKey, string apiSecret) => new()
    {
        ApiKey = apiKey,
        ApiSecret = apiSecret,
        Environment = "paper",
        Feed = "iex",
        ConnectTimeoutSeconds = 1,
        EventBufferCapacity = 16,
        ReconnectInitialDelaySeconds = 30,
        ReconnectMaxDelaySeconds = 30
    };

    private static async Task<RealtimeMarketDataEvent> ReadEventAsync(ChannelReader<RealtimeMarketDataEvent> events)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await events.ReadAsync(cts.Token);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        condition().ShouldBeTrue();
    }

    private sealed class RealtimeClientProbe(
        IAlpacaDataStreamingClient streamingClient,
        IAlpacaTradingClient tradingClient) : IAlpacaRealtimeClientFactory
    {
        public int ConnectCalls { get; private set; }

        public int DisconnectCalls { get; private set; }

        public static RealtimeClientProbe Create(AuthStatus authStatus)
        {
            var streamingClient = Substitute.For<IAlpacaDataStreamingClient>();
            streamingClient.ConnectAndAuthenticateAsync(Arg.Any<CancellationToken>()).Returns(authStatus);
            streamingClient.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

            var tradingClient = Substitute.For<IAlpacaTradingClient>();
            var marketClock = Substitute.For<global::Alpaca.Markets.IClock>();
            marketClock.IsOpen.Returns(true);
            marketClock.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 30, 0, DateTimeKind.Utc));
            marketClock.NextOpenUtc.Returns(new DateTime(2026, 3, 17, 13, 30, 0, DateTimeKind.Utc));
            marketClock.NextCloseUtc.Returns(new DateTime(2026, 3, 14, 20, 0, 0, DateTimeKind.Utc));
            tradingClient.GetClockAsync(Arg.Any<CancellationToken>()).Returns(marketClock);

            var probe = new RealtimeClientProbe(streamingClient, tradingClient);
            streamingClient.When(x => x.ConnectAndAuthenticateAsync(Arg.Any<CancellationToken>())).Do(_ => probe.ConnectCalls++);
            streamingClient.When(x => x.DisconnectAsync(Arg.Any<CancellationToken>())).Do(_ => probe.DisconnectCalls++);
            return probe;
        }

        public IAlpacaDataStreamingClient CreateDataStreamingClient(AlpacaRealtimeOptions options) => streamingClient;

        public IAlpacaTradingClient CreateTradingClient(AlpacaRealtimeOptions options) => tradingClient;
    }
}
