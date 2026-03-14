using NodaTime;
using System.Text.Json.Serialization;

namespace Aegis.Shared.Contracts.MarketData;

public static class MarketDataRealtimeContract
{
    public const string HubPath = "/hubs/market-data";
    public const string ContractVersion = "v1";

    public static class ScopeKinds
    {
        public const string Home = "home";
        public const string Watchlist = "watchlist";
    }

    public static class DeliveryStrategies
    {
        public const string RefreshHint = "refresh_hint";
        public const string CoalescedSnapshotDelta = "coalesced_snapshot_delta";
    }

    public static class EventNames
    {
        public const string HomeRefreshHint = "market_data_home_refresh_hint";
        public const string WatchlistSnapshot = "market_data_watchlist_snapshot";
    }

    public static class ChangeScopes
    {
        public const string BootstrapStatus = "bootstrap_status";
        public const string DailyReadiness = "daily_readiness";
        public const string IntradayReadiness = "intraday_readiness";
    }

    public static class GroupNames
    {
        public const string Home = "market-data:home";

        public static string Watchlist(Guid watchlistId) => $"market-data:watchlist:{watchlistId:D}";
    }
}

public sealed record MarketDataSubscriptionAck(
    [property: JsonPropertyName("contract_version")]
    string ContractVersion,
    [property: JsonPropertyName("scope_kind")]
    string ScopeKind,
    [property: JsonPropertyName("scope_key")]
    string ScopeKey,
    [property: JsonPropertyName("delivery_strategy")]
    string DeliveryStrategy,
    [property: JsonPropertyName("requires_authoritative_refresh")]
    bool RequiresAuthoritativeRefresh,
    [property: JsonPropertyName("subscribed_utc")]
    Instant SubscribedUtc);

public sealed record MarketDataWatchlistSubscriptionRequest
{
    public MarketDataWatchlistSubscriptionRequest()
    {
    }

    public MarketDataWatchlistSubscriptionRequest(Guid watchlistId)
    {
        WatchlistId = watchlistId;
    }

    [JsonPropertyName("watchlist_id")]
    public Guid WatchlistId { get; init; }
}

public sealed record MarketDataHomeRefreshEvent(
    [property: JsonPropertyName("contract_version")]
    string ContractVersion,
    [property: JsonPropertyName("event_id")]
    string EventId,
    [property: JsonPropertyName("occurred_utc")]
    Instant OccurredUtc,
    [property: JsonPropertyName("requires_refresh")]
    bool RequiresRefresh,
    [property: JsonPropertyName("changed_scopes")]
    IReadOnlyList<string> ChangedScopes);

public sealed record MarketDataWatchlistSymbolSnapshot(
    [property: JsonPropertyName("symbol")]
    string Symbol,
    [property: JsonPropertyName("current_price")]
    decimal? CurrentPrice,
    [property: JsonPropertyName("percent_change")]
    decimal? PercentChange);

public sealed record MarketDataWatchlistSnapshotEvent(
    [property: JsonPropertyName("contract_version")]
    string ContractVersion,
    [property: JsonPropertyName("event_id")]
    string EventId,
    [property: JsonPropertyName("watchlist_id")]
    Guid WatchlistId,
    [property: JsonPropertyName("batch_sequence")]
    long BatchSequence,
    [property: JsonPropertyName("occurred_utc")]
    Instant OccurredUtc,
    [property: JsonPropertyName("as_of_utc")]
    Instant AsOfUtc,
    [property: JsonPropertyName("requires_refresh")]
    bool RequiresRefresh,
    [property: JsonPropertyName("symbols")]
    IReadOnlyList<MarketDataWatchlistSymbolSnapshot> Symbols);
