using NodaTime;

namespace Aegis.Shared.Ports.MarketData;

public interface IHistoricalBarProvider
{
    Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken);

    Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken);
}

public sealed record HistoricalBarRequest(
    string Symbol,
    Instant? FromUtc = null,
    Instant? ToUtc = null,
    int? Limit = null,
    string? Feed = null);

public sealed record IntradayBarRequest(
    string Symbol,
    string Interval,
    Instant? FromUtc = null,
    Instant? ToUtc = null,
    int? Limit = null,
    string? Feed = null);

public sealed record HistoricalBarBatchResult(
    string Symbol,
    string Interval,
    IReadOnlyList<HistoricalBarRecord> Bars,
    string ProviderName,
    string? ProviderFeed,
    bool Succeeded,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static HistoricalBarBatchResult Success(
        string symbol,
        string interval,
        IReadOnlyList<HistoricalBarRecord> bars,
        string providerName,
        string? providerFeed) =>
        new(symbol, interval, bars, providerName, providerFeed, true, null, null);

    public static HistoricalBarBatchResult Failure(
        string symbol,
        string interval,
        string providerName,
        string? providerFeed,
        string errorCode,
        string errorMessage) =>
        new(symbol, interval, [], providerName, providerFeed, false, errorCode, errorMessage);
}

public sealed record HistoricalBarRecord(
    string Symbol,
    string Interval,
    Instant BarTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    string SessionType,
    LocalDate MarketDate,
    string RuntimeState,
    bool IsReconciled);
