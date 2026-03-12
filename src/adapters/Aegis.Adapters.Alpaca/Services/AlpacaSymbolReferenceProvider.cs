using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Shared.Ports.MarketData;

namespace Aegis.Adapters.Alpaca.Services;

public sealed class AlpacaSymbolReferenceProvider(HttpClient httpClient, AlpacaSymbolReferenceOptions options) : ISymbolReferenceProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ValidatedSymbolResult> ValidateSymbolAsync(ValidateSymbolRequest request, CancellationToken cancellationToken)
    {
        var normalizedSymbol = request.Symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return ValidatedSymbolResult.Invalid("invalid_symbol", "alpaca");
        }

        if (!string.Equals(request.AssetClass, "us_equities", StringComparison.OrdinalIgnoreCase))
        {
            return ValidatedSymbolResult.Invalid("unsupported_asset_class", "alpaca");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiSecret))
        {
            return ValidatedSymbolResult.Invalid("symbol_reference_unavailable", "alpaca");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v2/assets/{Uri.EscapeDataString(normalizedSymbol)}");
        httpRequest.Headers.TryAddWithoutValidation("APCA-API-KEY-ID", options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("APCA-API-SECRET-KEY", options.ApiSecret);

        using HttpResponseMessage? response = await SendRequestAsync(httpRequest, cancellationToken);
        if (response is null)
        {
            return ValidatedSymbolResult.Invalid("symbol_reference_unavailable", "alpaca");
        }

        // Distinguish a bad symbol from provider/transient failures so Universe can show the right user-facing error.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return ValidatedSymbolResult.Invalid("invalid_symbol", "alpaca");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ||
            (int)response.StatusCode == 429 ||
            (int)response.StatusCode >= 500)
        {
            return ValidatedSymbolResult.Invalid("symbol_reference_unavailable", "alpaca");
        }

        if (!response.IsSuccessStatusCode)
        {
            return ValidatedSymbolResult.Invalid("symbol_reference_unavailable", "alpaca");
        }

        AlpacaAssetResponse? asset;
        try
        {
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            asset = await JsonSerializer.DeserializeAsync<AlpacaAssetResponse>(responseStream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return ValidatedSymbolResult.Invalid("symbol_reference_unavailable", "alpaca");
        }

        if (asset is null || string.IsNullOrWhiteSpace(asset.Symbol))
        {
            return ValidatedSymbolResult.Invalid("symbol_reference_unavailable", "alpaca");
        }

        if (!string.Equals(asset.AssetClass, "us_equity", StringComparison.OrdinalIgnoreCase))
        {
            return ValidatedSymbolResult.Invalid("unsupported_asset_class", "alpaca");
        }

        return ValidatedSymbolResult.Valid(
            asset.Symbol.Trim().ToUpperInvariant(),
            "us_equities",
            "alpaca",
            asset.Name,
            asset.Exchange);
    }

    private async Task<HttpResponseMessage?> SendRequestAsync(HttpRequestMessage httpRequest, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

    private sealed class AlpacaAssetResponse
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; init; }

        [JsonPropertyName("class")]
        public string? AssetClass { get; init; }

        [JsonPropertyName("exchange")]
        public string? Exchange { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
