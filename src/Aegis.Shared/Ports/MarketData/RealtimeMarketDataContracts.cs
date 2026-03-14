using System.Threading.Channels;
using NodaTime;

namespace Aegis.Shared.Ports.MarketData;

public interface IRealtimeMarketDataProvider : IAsyncDisposable
{
    // The adapter owns reconnects and pushes normalized events through one bounded stream so MarketData can consume
    // provider-agnostic messages without binding itself to vendor subscription or websocket behavior.
    ChannelReader<RealtimeMarketDataEvent> Events { get; }

    Task StartAsync(CancellationToken cancellationToken);

    // Subscription updates use replace-all semantics; adapters translate that desired state into provider-native diffs.
    Task ApplySubscriptionSnapshotAsync(RealtimeMarketDataSubscriptionSet subscriptionSet, CancellationToken cancellationToken);

    ValueTask<RealtimeMarketDataProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

public sealed record RealtimeMarketDataSubscriptionSet(
    IReadOnlyCollection<string> Trades,
    IReadOnlyCollection<string> Quotes,
    IReadOnlyCollection<string> MinuteBars,
    IReadOnlyCollection<string> UpdatedBars,
    IReadOnlyCollection<string> DailyBars,
    IReadOnlyCollection<string> TradingStatuses,
    IReadOnlyCollection<string> TradeCorrections,
    IReadOnlyCollection<string> TradeCancels)
{
    public static RealtimeMarketDataSubscriptionSet Empty { get; } = new([], [], [], [], [], [], [], []);
}

public sealed record RealtimeMarketDataProviderCapabilities(
    string ProviderName,
    string DefaultFeed,
    IReadOnlyCollection<string> SupportedFeeds,
    IReadOnlyCollection<string> SupportedIntervals,
    bool SupportsHistoricalBatches,
    bool SupportsRevisionEvents,
    bool SupportsIncrementalSubscriptionChanges,
    bool SupportsPartialSubscriptionFailures,
    int? MaxSymbolsPerIncrementalSubscriptionChange,
    bool SupportsTrades,
    bool SupportsQuotes,
    bool SupportsMinuteBars,
    bool SupportsUpdatedBars,
    bool SupportsDailyBars,
    bool SupportsTradingStatuses,
    bool SupportsTradeCorrections,
    bool SupportsTradeCancels,
    bool SupportsMarketStatus,
    bool SupportsProviderStatus,
    bool SupportsErrorSignals);

public abstract record RealtimeMarketDataEvent(
    string EventType,
    Instant ReceivedUtc,
    string ProviderName,
    string? ProviderFeed);

public sealed record RealtimeFinalizedBarEvent(
    string Symbol,
    string Interval,
    Instant ReceivedUtc,
    Instant BarTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    decimal? Vwap,
    long? TradeCount,
    string ProviderName,
    string? ProviderFeed)
    : RealtimeMarketDataEvent("finalized_bar", ReceivedUtc, ProviderName, ProviderFeed);

public sealed record RealtimeUpdatedBarEvent(
    string Symbol,
    string Interval,
    Instant ReceivedUtc,
    Instant BarTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    decimal? Vwap,
    long? TradeCount,
    string ProviderName,
    string? ProviderFeed)
    : RealtimeMarketDataEvent("updated_bar", ReceivedUtc, ProviderName, ProviderFeed);

public sealed record RealtimeTradeSnapshot(
    string Symbol,
    Instant TradeTimeUtc,
    decimal Price,
    decimal Size,
    string? TradeId,
    string? Exchange,
    string? Tape,
    string? UpdateReason,
    IReadOnlyList<string> Conditions);

public sealed record RealtimeTradeEvent(
    RealtimeTradeSnapshot Trade,
    Instant ReceivedUtc,
    string ProviderName,
    string? ProviderFeed)
    : RealtimeMarketDataEvent("trade", ReceivedUtc, ProviderName, ProviderFeed);

public sealed record RealtimeQuoteEvent(
    string Symbol,
    Instant QuoteTimeUtc,
    decimal BidPrice,
    decimal AskPrice,
    decimal BidSize,
    decimal AskSize,
    string? BidExchange,
    string? AskExchange,
    string? Tape,
    IReadOnlyList<string> Conditions,
    Instant ReceivedUtc,
    string ProviderName,
    string? ProviderFeed)
    : RealtimeMarketDataEvent("quote", ReceivedUtc, ProviderName, ProviderFeed);

public sealed record RealtimeTradingStatusEvent(
    string Symbol,
    Instant StatusTimeUtc,
    string StatusCode,
    string? StatusMessage,
    string? ReasonCode,
    string? ReasonMessage,
    string? Tape,
    Instant ReceivedUtc,
    string ProviderName,
    string? ProviderFeed)
    : RealtimeMarketDataEvent("trading_status", ReceivedUtc, ProviderName, ProviderFeed);

public sealed record RealtimeMarketStatusEvent(
    bool IsOpen,
    Instant StatusTimeUtc,
    Instant NextOpenUtc,
    Instant NextCloseUtc,
    Instant ReceivedUtc,
    string ProviderName,
    string? ProviderFeed)
    : RealtimeMarketDataEvent("market_status", ReceivedUtc, ProviderName, ProviderFeed);

public sealed record RealtimeProviderStatusEvent(
    string StatusCode,
    string StatusMessage,
    bool IsConnected,
    bool IsAuthenticated,
    bool IsTerminal,
    Instant ReceivedUtc,
    string ProviderName,
    string? ProviderFeed)
    : RealtimeMarketDataEvent("provider_status", ReceivedUtc, ProviderName, ProviderFeed);

public sealed record RealtimeTradeCorrectionEvent(
    RealtimeTradeSnapshot OriginalTrade,
    RealtimeTradeSnapshot CorrectedTrade,
    Instant ReceivedUtc,
    string ProviderName,
    string? ProviderFeed)
    : RealtimeMarketDataEvent("trade_correction", ReceivedUtc, ProviderName, ProviderFeed);

public sealed record RealtimeTradeCancelEvent(
    RealtimeTradeSnapshot CancelledTrade,
    Instant ReceivedUtc,
    string ProviderName,
    string? ProviderFeed)
    : RealtimeMarketDataEvent("trade_cancel", ReceivedUtc, ProviderName, ProviderFeed);

public sealed record RealtimeProviderErrorEvent(
    string ErrorCode,
    string ErrorMessage,
    bool IsTransient,
    string? Symbol,
    Instant ReceivedUtc,
    string ProviderName,
    string? ProviderFeed)
    : RealtimeMarketDataEvent("provider_error", ReceivedUtc, ProviderName, ProviderFeed);
