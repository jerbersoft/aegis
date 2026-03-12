using System.Net;
using System.Text;
using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Adapters.Alpaca.Services;
using Aegis.Shared.Ports.MarketData;
using Shouldly;

namespace Aegis.Universe.UnitTests;

public sealed class AlpacaSymbolReferenceProviderTests
{
    [Fact]
    public async Task ValidateSymbolAsync_ShouldReturnInvalid_WhenSymbolIsBlank()
    {
        var provider = CreateProvider(_ => throw new InvalidOperationException("HTTP should not be called for blank symbols."));

        var result = await provider.ValidateSymbolAsync(new ValidateSymbolRequest("   "), CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid_symbol");
        result.ProviderName.ShouldBe("alpaca");
    }

    [Fact]
    public async Task ValidateSymbolAsync_ShouldReturnUnsupportedAssetClass_WhenRequestUsesUnsupportedAssetClass()
    {
        var provider = CreateProvider(_ => throw new InvalidOperationException("HTTP should not be called for unsupported asset classes."));

        var result = await provider.ValidateSymbolAsync(new ValidateSymbolRequest("AAPL", "crypto"), CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("unsupported_asset_class");
        result.ProviderName.ShouldBe("alpaca");
    }

    [Fact]
    public async Task ValidateSymbolAsync_ShouldMapSuccessfulAssetResponse()
    {
        var provider = CreateProvider(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "symbol": "AAPL",
                  "class": "us_equity",
                  "exchange": "NASDAQ",
                  "name": "Apple Inc."
                }
                """, Encoding.UTF8, "application/json")
            });

        var result = await provider.ValidateSymbolAsync(new ValidateSymbolRequest("aapl"), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.NormalizedSymbol.ShouldBe("AAPL");
        result.AssetClass.ShouldBe("us_equities");
        result.ProviderName.ShouldBe("alpaca");
        result.DisplayName.ShouldBe("Apple Inc.");
        result.Exchange.ShouldBe("NASDAQ");
        result.ReasonCode.ShouldBe("none");
    }

    [Fact]
    public async Task ValidateSymbolAsync_ShouldReturnInvalid_WhenProviderReturnsNotFound()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await provider.ValidateSymbolAsync(new ValidateSymbolRequest("NOPE"), CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid_symbol");
        result.ProviderName.ShouldBe("alpaca");
    }

    [Fact]
    public async Task ValidateSymbolAsync_ShouldReturnUnavailable_WhenProviderFails()
    {
        var provider = CreateProvider(_ => throw new HttpRequestException("boom"));

        var result = await provider.ValidateSymbolAsync(new ValidateSymbolRequest("AAPL"), CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.ReasonCode.ShouldBe("symbol_reference_unavailable");
        result.ProviderName.ShouldBe("alpaca");
    }

    private static AlpacaSymbolReferenceProvider CreateProvider(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://paper-api.alpaca.markets/")
        };

        return new AlpacaSymbolReferenceProvider(httpClient, new AlpacaSymbolReferenceOptions
        {
            ApiKey = "key",
            ApiSecret = "secret"
        });
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
