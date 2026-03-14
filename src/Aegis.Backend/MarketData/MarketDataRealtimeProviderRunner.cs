using Aegis.Shared.Ports.MarketData;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Backend.MarketData;

public sealed class MarketDataRealtimeProviderRunner(
    IRealtimeMarketDataProvider provider,
    IOptions<MarketDataRealtimeOptions> options,
    ILogger<MarketDataRealtimeProviderRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableProviderRuntime)
        {
            logger.LogInformation("MarketData realtime provider runtime is disabled by configuration.");
            return;
        }

        try
        {
            await provider.StartAsync(stoppingToken);
            logger.LogInformation("MarketData realtime provider runtime started.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "MarketData realtime provider runtime failed to start.");
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (options.Value.EnableProviderRuntime)
        {
            await provider.StopAsync(cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}
