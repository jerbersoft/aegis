using Alpaca.Markets;
using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Shared.Ports.MarketData;

namespace Aegis.Adapters.Alpaca.Services;

internal static class AlpacaRealtimeContractResolver
{
    private static readonly string[] SupportedFeeds = ["iex", "sip", "otc"];
    private static readonly string[] SupportedIntervals = ["1min", "1day"];

    public static IEnvironment ResolveEnvironment(string? environment) => NormalizeEnvironment(environment) switch
    {
        "live" => Environments.Live,
        _ => Environments.Paper
    };

    public static MarketDataFeed ResolveFeed(string? feed) => NormalizeFeed(feed) switch
    {
        "sip" => MarketDataFeed.Sip,
        "otc" => MarketDataFeed.Otc,
        _ => MarketDataFeed.Iex
    };

    public static string NormalizeEnvironment(string? environment)
    {
        var normalized = (environment ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "" or "paper" => "paper",
            "live" => "live",
            _ => throw new InvalidOperationException($"Unsupported Alpaca environment '{environment}'.")
        };
    }

    public static string NormalizeFeed(string? feed)
    {
        var normalized = (feed ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "" or "iex" => "iex",
            "sip" => "sip",
            "otc" => "otc",
            _ => throw new InvalidOperationException($"Unsupported Alpaca feed '{feed}'.")
        };
    }

    public static RealtimeMarketDataProviderCapabilities BuildCapabilities(AlpacaRealtimeOptions options) => new(
        ProviderName: "alpaca",
        DefaultFeed: NormalizeFeed(options.Feed),
        SupportedFeeds: SupportedFeeds,
        SupportedIntervals: SupportedIntervals,
        SupportsHistoricalBatches: true,
        SupportsRevisionEvents: true,
        SupportsIncrementalSubscriptionChanges: true,
        SupportsPartialSubscriptionFailures: false,
        MaxSymbolsPerIncrementalSubscriptionChange: null,
        SupportsTrades: true,
        SupportsQuotes: true,
        SupportsMinuteBars: true,
        SupportsUpdatedBars: true,
        SupportsDailyBars: true,
        SupportsTradingStatuses: true,
        SupportsTradeCorrections: true,
        SupportsTradeCancels: true,
        SupportsMarketStatus: true,
        SupportsProviderStatus: true,
        SupportsErrorSignals: true);
}
