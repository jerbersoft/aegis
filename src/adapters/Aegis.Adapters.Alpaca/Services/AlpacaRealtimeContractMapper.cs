using System.Collections;
using System.Globalization;
using System.Net.Sockets;
using System.Net.WebSockets;
using Alpaca.Markets;
using Aegis.Shared.Ports.MarketData;
using NodaTime;

namespace Aegis.Adapters.Alpaca.Services;

internal static class AlpacaRealtimeContractMapper
{
    private const string ProviderName = "alpaca";

    public static RealtimeFinalizedBarEvent MapMinuteBar(IBar bar, string? providerFeed, Instant receivedUtc) =>
        CreateFinalizedBarEvent(bar, "1min", providerFeed, receivedUtc);

    public static RealtimeFinalizedBarEvent MapDailyBar(IBar bar, string? providerFeed, Instant receivedUtc) =>
        CreateFinalizedBarEvent(bar, "1day", providerFeed, receivedUtc);

    public static RealtimeUpdatedBarEvent MapUpdatedBar(IBar bar, string? providerFeed, Instant receivedUtc) =>
        new(
            NormalizeSymbol(bar.Symbol),
            "1min",
            receivedUtc,
            ToInstant(bar.TimeUtc),
            ToDecimal(bar.Open),
            ToDecimal(bar.High),
            ToDecimal(bar.Low),
            ToDecimal(bar.Close),
            ToInt64(bar.Volume),
            ToNullableDecimal(bar.Vwap),
            ToNullableInt64(bar.TradeCount),
            ProviderName,
            NormalizeProviderFeed(providerFeed));

    public static RealtimeTradeEvent MapTrade(ITrade trade, string? providerFeed, Instant receivedUtc) =>
        new(CreateTradeSnapshot(trade), receivedUtc, ProviderName, NormalizeProviderFeed(providerFeed));

    public static RealtimeTradeCancelEvent MapTradeCancel(ITrade trade, string? providerFeed, Instant receivedUtc) =>
        new(CreateTradeSnapshot(trade), receivedUtc, ProviderName, NormalizeProviderFeed(providerFeed));

    public static RealtimeTradeCorrectionEvent MapTradeCorrection(ICorrection correction, string? providerFeed, Instant receivedUtc) =>
        new(
            CreateTradeSnapshot(correction.OriginalTrade),
            CreateTradeSnapshot(correction.CorrectedTrade),
            receivedUtc,
            ProviderName,
            NormalizeProviderFeed(providerFeed));

    public static RealtimeQuoteEvent MapQuote(IQuote quote, string? providerFeed, Instant receivedUtc) =>
        new(
            NormalizeSymbol(quote.Symbol),
            ToInstant(quote.TimestampUtc),
            ToDecimal(quote.BidPrice),
            ToDecimal(quote.AskPrice),
            ToDecimal(quote.BidSize),
            ToDecimal(quote.AskSize),
            NormalizeNullableString(quote.BidExchange),
            NormalizeNullableString(quote.AskExchange),
            NormalizeNullableString(quote.Tape),
            NormalizeCodes((IEnumerable?)quote.Conditions),
            receivedUtc,
            ProviderName,
            NormalizeProviderFeed(providerFeed));

    public static RealtimeTradingStatusEvent MapTradingStatus(IStatus status, string? providerFeed, Instant receivedUtc) =>
        new(
            NormalizeSymbol(status.Symbol),
            ToInstant(status.TimestampUtc),
            NormalizeNullableString(status.StatusCode) ?? "unknown",
            NormalizeNullableString(status.StatusMessage),
            NormalizeNullableString(status.ReasonCode),
            NormalizeNullableString(status.ReasonMessage),
            NormalizeNullableString(status.Tape),
            receivedUtc,
            ProviderName,
            NormalizeProviderFeed(providerFeed));

    public static RealtimeMarketStatusEvent MapMarketStatus(global::Alpaca.Markets.IClock clock, string? providerFeed, Instant receivedUtc) =>
        new(
            clock.IsOpen,
            ToInstant(clock.TimestampUtc),
            ToInstant(clock.NextOpenUtc),
            ToInstant(clock.NextCloseUtc),
            receivedUtc,
            ProviderName,
            NormalizeProviderFeed(providerFeed));

    public static RealtimeProviderStatusEvent MapProviderStatus(string statusCode, string? providerFeed, Instant receivedUtc) =>
        new(
            NormalizeStatusCode(statusCode),
            GetProviderStatusMessage(statusCode),
            IsConnected(statusCode),
            IsAuthenticated(statusCode),
            IsTerminal(statusCode),
            receivedUtc,
            ProviderName,
            NormalizeProviderFeed(providerFeed));

    public static RealtimeProviderErrorEvent MapProviderError(Exception exception, string? providerFeed, Instant receivedUtc, string? symbol = null) =>
        new(
            NormalizeErrorCode(exception),
            exception.Message,
            IsTransient(exception),
            string.IsNullOrWhiteSpace(symbol) ? null : NormalizeSymbol(symbol),
            receivedUtc,
            ProviderName,
            NormalizeProviderFeed(providerFeed));

    private static RealtimeFinalizedBarEvent CreateFinalizedBarEvent(IBar bar, string interval, string? providerFeed, Instant receivedUtc) =>
        new(
            NormalizeSymbol(bar.Symbol),
            interval,
            receivedUtc,
            ToInstant(bar.TimeUtc),
            ToDecimal(bar.Open),
            ToDecimal(bar.High),
            ToDecimal(bar.Low),
            ToDecimal(bar.Close),
            ToInt64(bar.Volume),
            ToNullableDecimal(bar.Vwap),
            ToNullableInt64(bar.TradeCount),
            ProviderName,
            NormalizeProviderFeed(providerFeed));

    private static RealtimeTradeSnapshot CreateTradeSnapshot(ITrade trade) =>
        new(
            NormalizeSymbol(trade.Symbol),
            ToInstant(trade.TimestampUtc),
            ToDecimal(trade.Price),
            ToDecimal(trade.Size),
            NormalizeNullableString(trade.TradeId),
            NormalizeNullableString(trade.Exchange),
            NormalizeNullableString(trade.Tape),
            NormalizeNullableString(trade.Update),
            NormalizeCodes((IEnumerable?)trade.Conditions));

    private static string NormalizeSymbol(string symbol) => symbol.Trim().ToUpperInvariant();

    private static string? NormalizeProviderFeed(string? providerFeed) =>
        string.IsNullOrWhiteSpace(providerFeed) ? null : providerFeed.Trim().ToLowerInvariant();

    private static string? NormalizeNullableString(object? value)
    {
        var normalized = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static IReadOnlyList<string> NormalizeCodes(IEnumerable? values)
    {
        if (values is null)
        {
            return [];
        }

        var normalized = new List<string>();
        foreach (var value in values)
        {
            var code = NormalizeNullableString(value);
            if (code is not null)
            {
                normalized.Add(code);
            }
        }

        return normalized;
    }

    private static Instant ToInstant(object value) => value switch
    {
        DateTime utcDateTime => Instant.FromDateTimeUtc(utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)),
        DateTimeOffset dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset),
        _ => throw new InvalidOperationException($"Unsupported Alpaca timestamp type '{value.GetType().FullName}'.")
    };

    private static decimal ToDecimal<TValue>(TValue value) => Convert.ToDecimal(value, CultureInfo.InvariantCulture);

    private static decimal? ToNullableDecimal<TValue>(TValue value)
    {
        var boxed = value as object;
        return boxed is null ? null : Convert.ToDecimal(boxed, CultureInfo.InvariantCulture);
    }

    private static long ToInt64<TValue>(TValue value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);

    private static long? ToNullableInt64<TValue>(TValue value)
    {
        var boxed = value as object;
        return boxed is null ? null : Convert.ToInt64(boxed, CultureInfo.InvariantCulture);
    }

    private static string NormalizeErrorCode(Exception exception) => exception switch
    {
        _ when ContainsAny(exception.Message, "rate limit", "too many requests") => "rate_limited",
        _ when ContainsAny(exception.Message, "too many", "limit exceeded", "subscription limit") => "subscription_limit_exceeded",
        _ when ContainsAny(exception.Message, "invalid symbol", "unknown symbol", "not permitted", "rejected") => "subscription_rejected",
        InvalidOperationException => "invalid_operation",
        HttpRequestException => "http_request_failed",
        SocketException => "socket_error",
        WebSocketException => "websocket_error",
        TimeoutException => "timeout",
        OperationCanceledException => "cancelled",
        _ => exception.GetType().Name.ToLowerInvariant()
    };

    private static bool IsTransient(Exception exception) => exception switch
    {
        OperationCanceledException => false,
        InvalidOperationException => false,
        TimeoutException => true,
        HttpRequestException => true,
        SocketException => true,
        WebSocketException => true,
        _ when ContainsAny(exception.Message, "rate limit", "too many requests") => true,
        _ when ContainsAny(exception.Message, "too many", "limit exceeded", "subscription limit") => false,
        _ when ContainsAny(exception.Message, "invalid symbol", "unknown symbol", "not permitted", "rejected") => false,
        _ => true
    };

    private static bool ContainsAny(string? value, params string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeStatusCode(string statusCode)
    {
        if (string.IsNullOrWhiteSpace(statusCode))
        {
            throw new InvalidOperationException("Provider status code is required.");
        }

        return statusCode.Trim();
    }

    private static bool IsConnected(string statusCode)
    {
        var normalized = NormalizeStatusCode(statusCode);
        return normalized is "Connected" or "Success";
    }

    private static bool IsAuthenticated(string statusCode)
    {
        var normalized = NormalizeStatusCode(statusCode);
        return normalized is "AuthenticationSuccess" or "AlpacaDataStreamingAuthorized" or "Authenticated";
    }

    private static bool IsTerminal(string statusCode)
    {
        var normalized = NormalizeStatusCode(statusCode);
        return normalized is "AuthenticationFailed" or "AlpacaDataStreamingUnauthorized" or "Failed";
    }

    private static string GetProviderStatusMessage(string statusCode)
    {
        var normalized = NormalizeStatusCode(statusCode);
        return normalized switch
        {
            "Connected" => "stream_connected",
            "ConfigurationInvalid" => "configuration_invalid",
            "AuthenticationRequired" => "authentication_required",
            "AuthenticationSuccess" => "authentication_succeeded",
            "AuthenticationFailed" => "authentication_failed",
            "AlpacaDataStreamingAuthorized" => "authentication_succeeded",
            "AlpacaDataStreamingUnauthorized" => "authentication_failed",
            "Authenticated" => "authentication_completed",
            "Success" => "operation_succeeded",
            "Failed" => "operation_failed",
            _ => "status_changed"
        };
    }
}
