using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Shared.Ports.MarketData;

namespace Aegis.Adapters.Alpaca.Services;

public sealed class AlpacaHistoricalBarProvider(HttpClient httpClient, AlpacaHistoricalDataOptions options) : IHistoricalBarProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken)
    {
        var normalizedSymbol = request.Symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return HistoricalBarBatchResult.Failure(normalizedSymbol, "1day", "alpaca", options.Feed, "invalid_symbol", "A symbol is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiSecret))
        {
            return HistoricalBarBatchResult.Failure(normalizedSymbol, "1day", "alpaca", options.Feed, "historical_data_unavailable", "Historical data credentials are unavailable.");
        }

        var queryParts = new List<string>
        {
            "timeframe=1Day",
            "adjustment=raw",
            $"feed={Uri.EscapeDataString(request.Feed ?? options.Feed)}"
        };

        if (request.Limit is { } limit && limit > 0)
        {
            queryParts.Add($"limit={limit}");
        }

        if (request.FromUtc is { } fromUtc)
        {
            queryParts.Add($"start={Uri.EscapeDataString(fromUtc.UtcDateTime.ToString("O"))}");
        }

        if (request.ToUtc is { } toUtc)
        {
            queryParts.Add($"end={Uri.EscapeDataString(toUtc.UtcDateTime.ToString("O"))}");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v2/stocks/{Uri.EscapeDataString(normalizedSymbol)}/bars?{string.Join("&", queryParts)}");
        httpRequest.Headers.TryAddWithoutValidation("APCA-API-KEY-ID", options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("APCA-API-SECRET-KEY", options.ApiSecret);

        using var response = await SendRequestAsync(httpRequest, cancellationToken);
        if (response is null)
        {
            return HistoricalBarBatchResult.Failure(normalizedSymbol, "1day", "alpaca", options.Feed, "historical_data_unavailable", "Historical data is currently unavailable.");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ||
            (int)response.StatusCode == 429 ||
            (int)response.StatusCode >= 500)
        {
            return HistoricalBarBatchResult.Failure(normalizedSymbol, "1day", "alpaca", options.Feed, "historical_data_unavailable", "Historical data is currently unavailable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return HistoricalBarBatchResult.Failure(normalizedSymbol, "1day", "alpaca", options.Feed, "historical_data_unavailable", "Historical data request failed.");
        }

        AlpacaHistoricalBarsResponse? payload;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            payload = await JsonSerializer.DeserializeAsync<AlpacaHistoricalBarsResponse>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return HistoricalBarBatchResult.Failure(normalizedSymbol, "1day", "alpaca", options.Feed, "historical_data_unavailable", "Historical data response was invalid.");
        }

        var bars = (payload?.Bars ?? [])
            .Select(bar => new HistoricalBarRecord(
                normalizedSymbol,
                "1day",
                DateTimeOffset.Parse(bar.Timestamp!, null, System.Globalization.DateTimeStyles.AssumeUniversal),
                bar.Open,
                bar.High,
                bar.Low,
                bar.Close,
                bar.Volume,
                "regular",
                DateOnly.FromDateTime(DateTimeOffset.Parse(bar.Timestamp!, null, System.Globalization.DateTimeStyles.AssumeUniversal).UtcDateTime),
                "reconciled",
                true))
            .OrderBy(x => x.BarTimeUtc)
            .ToArray();

        return HistoricalBarBatchResult.Success(normalizedSymbol, "1day", bars, "alpaca", request.Feed ?? options.Feed);
    }

    private async Task<HttpResponseMessage?> SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private sealed class AlpacaHistoricalBarsResponse
    {
        [JsonPropertyName("bars")]
        public IReadOnlyList<AlpacaBarResponse>? Bars { get; init; }
    }

    private sealed class AlpacaBarResponse
    {
        [JsonPropertyName("o")]
        public decimal Open { get; init; }

        [JsonPropertyName("h")]
        public decimal High { get; init; }

        [JsonPropertyName("l")]
        public decimal Low { get; init; }

        [JsonPropertyName("c")]
        public decimal Close { get; init; }

        [JsonPropertyName("v")]
        public long Volume { get; init; }

        [JsonPropertyName("t")]
        public string? Timestamp { get; init; }
    }
}
