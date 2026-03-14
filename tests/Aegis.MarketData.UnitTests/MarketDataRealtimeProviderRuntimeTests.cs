using System.Threading.Channels;
using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Adapters.Alpaca.Services;
using Aegis.Backend.MarketData;
using Aegis.Shared.Ports.MarketData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;
using Shouldly;

namespace Aegis.MarketData.UnitTests;

public sealed class MarketDataRealtimeProviderRuntimeTests
{
    [Fact]
    public async Task AddMarketDataRealtimeProviderRuntime_ShouldRegisterSharedRealtimeProviderWithoutVendorLeakage()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.Configure<MarketDataRealtimeOptions>(_ => { });

        services.AddMarketDataRealtimeProviderRuntime(new AlpacaRealtimeOptions
        {
            ApiKey = "key",
            ApiSecret = "secret"
        });

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        provider.GetRequiredService<IRealtimeMarketDataProvider>().ShouldBeOfType<AlpacaRealtimeMarketDataProvider>();
        provider.GetServices<IHostedService>().ShouldContain(x => x is MarketDataRealtimeProviderRunner);
        provider.GetRequiredService<AlpacaRealtimeOptions>().ApiKey.ShouldBe("key");
    }

    [Fact]
    public async Task HostedRunner_ShouldStartAndStopProvider_WhenRuntimeEnabled()
    {
        await using var realtimeProvider = new RecordingRealtimeProvider();
        var runner = new MarketDataRealtimeProviderRunner(
            realtimeProvider,
            Options.Create(new MarketDataRealtimeOptions { EnableProviderRuntime = true }),
            NullLogger<MarketDataRealtimeProviderRunner>.Instance);

        await runner.StartAsync(CancellationToken.None);
        await ShouldCompleteEventuallyAsync(() => realtimeProvider.StartCalls == 1);
        realtimeProvider.StartCalls.ShouldBe(1);

        await runner.StopAsync(CancellationToken.None);
        realtimeProvider.StopCalls.ShouldBe(1);
    }

    [Fact]
    public async Task HostedRunner_ShouldNotStartOrStopProvider_WhenRuntimeDisabled()
    {
        await using var realtimeProvider = new RecordingRealtimeProvider();
        var runner = new MarketDataRealtimeProviderRunner(
            realtimeProvider,
            Options.Create(new MarketDataRealtimeOptions { EnableProviderRuntime = false }),
            NullLogger<MarketDataRealtimeProviderRunner>.Instance);

        await runner.StartAsync(CancellationToken.None);
        await runner.StopAsync(CancellationToken.None);

        realtimeProvider.StartCalls.ShouldBe(0);
        realtimeProvider.StopCalls.ShouldBe(0);
    }

    [Fact]
    public async Task HostedRunner_ShouldLeaveConfigurationFailureEventsAvailableThroughProviderStream()
    {
        await using var realtimeProvider = new FailingRealtimeProvider();
        var runner = new MarketDataRealtimeProviderRunner(
            realtimeProvider,
            Options.Create(new MarketDataRealtimeOptions { EnableProviderRuntime = true }),
            NullLogger<MarketDataRealtimeProviderRunner>.Instance);

        await runner.StartAsync(CancellationToken.None);
        await ShouldCompleteEventuallyAsync(() => realtimeProvider.StartCalls == 1);

        var statusEvent = await realtimeProvider.Events.ReadAsync(CancellationToken.None);
        statusEvent.ShouldBeOfType<RealtimeProviderStatusEvent>().StatusCode.ShouldBe("ConfigurationInvalid");

        var errorEvent = await realtimeProvider.Events.ReadAsync(CancellationToken.None);
        errorEvent.ShouldBeOfType<RealtimeProviderErrorEvent>().ErrorCode.ShouldBe("invalid_operation");

        await runner.StopAsync(CancellationToken.None);
    }

    private static async Task ShouldCompleteEventuallyAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        condition().ShouldBeTrue();
    }

    private sealed class RecordingRealtimeProvider : IRealtimeMarketDataProvider
    {
        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public ChannelReader<RealtimeMarketDataEvent> Events => Channel.CreateUnbounded<RealtimeMarketDataEvent>().Reader;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCalls++;
            return Task.CompletedTask;
        }

        public Task ApplySubscriptionSnapshotAsync(RealtimeMarketDataSubscriptionSet subscriptionSet, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask<RealtimeMarketDataProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new RealtimeMarketDataProviderCapabilities(
                "fake",
                "iex",
                ["iex"],
                ["1min"],
                false,
                false,
                false,
                false,
                null,
                true,
                true,
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                true));

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCalls++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailingRealtimeProvider : IRealtimeMarketDataProvider
    {
        private readonly Channel<RealtimeMarketDataEvent> _events = Channel.CreateUnbounded<RealtimeMarketDataEvent>();

        public int StartCalls { get; private set; }

        public ChannelReader<RealtimeMarketDataEvent> Events => _events.Reader;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCalls++;
            _events.Writer.TryWrite(new RealtimeProviderStatusEvent("ConfigurationInvalid", "configuration_invalid", false, false, false, SystemClock.Instance.GetCurrentInstant(), "alpaca", null));
            _events.Writer.TryWrite(new RealtimeProviderErrorEvent("invalid_operation", "Alpaca realtime credentials are required.", false, null, SystemClock.Instance.GetCurrentInstant(), "alpaca", null));
            throw new InvalidOperationException("Alpaca realtime credentials are required.");
        }

        public Task ApplySubscriptionSnapshotAsync(RealtimeMarketDataSubscriptionSet subscriptionSet, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask<RealtimeMarketDataProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new RealtimeMarketDataProviderCapabilities(
                "fake",
                "iex",
                ["iex"],
                ["1min"],
                false,
                false,
                false,
                false,
                null,
                true,
                true,
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                true));

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            _events.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
