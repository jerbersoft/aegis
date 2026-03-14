using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Adapters.Alpaca.Services;
using Aegis.Shared.Ports.MarketData;

namespace Aegis.Backend.MarketData;

public static class MarketDataRealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddMarketDataRealtimeProviderRuntime(
        this IServiceCollection services,
        AlpacaRealtimeOptions options)
    {
        services.AddOptions();
        services.AddSingleton(options);
        services.AddSingleton<IAlpacaRealtimeClientFactory, AlpacaRealtimeClientFactory>();
        services.AddSingleton<IRealtimeMarketDataProvider, AlpacaRealtimeMarketDataProvider>();
        services.AddHostedService<MarketDataRealtimeProviderRunner>();
        return services;
    }
}
