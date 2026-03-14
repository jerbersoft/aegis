using Alpaca.Markets;
using Alpaca.Markets.Extensions;
using Aegis.Adapters.Alpaca.Configuration;

namespace Aegis.Adapters.Alpaca.Services;

public sealed class AlpacaRealtimeClientFactory : IAlpacaRealtimeClientFactory
{
    public IAlpacaDataStreamingClient CreateDataStreamingClient(AlpacaRealtimeOptions options)
    {
        var configuration = BuildDataStreamingClientConfiguration(options);
        return ConfigurationExtensions.GetClient(configuration);
    }

    public IAlpacaTradingClient CreateTradingClient(AlpacaRealtimeOptions options)
    {
        var environment = AlpacaRealtimeContractResolver.ResolveEnvironment(options.Environment);
        return environment.GetAlpacaTradingClient(CreateSecurityKey(options));
    }

    internal static AlpacaDataStreamingClientConfiguration BuildDataStreamingClientConfiguration(AlpacaRealtimeOptions options)
    {
        var environment = AlpacaRealtimeContractResolver.ResolveEnvironment(options.Environment);
        var configuration = environment.GetAlpacaDataStreamingClientConfiguration(CreateSecurityKey(options));
        var resolvedFeed = AlpacaRealtimeContractResolver.NormalizeFeed(options.Feed);

        // Alpaca's environment helpers pick a default feed-specific websocket path, but this adapter must override that
        // endpoint whenever configuration requests a different feed so runtime behavior matches reported capabilities.
        configuration.ApiEndpoint = BuildFeedScopedEndpoint(configuration.ApiEndpoint, resolvedFeed);
        return configuration;
    }

    private static SecretKey CreateSecurityKey(AlpacaRealtimeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiSecret))
        {
            throw new InvalidOperationException("Alpaca realtime credentials are required.");
        }

        return new SecretKey(options.ApiKey, options.ApiSecret);
    }

    private static Uri BuildFeedScopedEndpoint(Uri baseEndpoint, string resolvedFeed)
    {
        var segments = baseEndpoint.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        if (segments.Length == 0)
        {
            throw new InvalidOperationException($"Unsupported Alpaca streaming endpoint '{baseEndpoint}'.");
        }

        segments[^1] = resolvedFeed;

        var builder = new UriBuilder(baseEndpoint)
        {
            Path = string.Join('/', segments)
        };

        return builder.Uri;
    }
}
