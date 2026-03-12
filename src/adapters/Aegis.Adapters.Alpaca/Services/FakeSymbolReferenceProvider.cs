using Aegis.Shared.Ports.MarketData;

namespace Aegis.Adapters.Alpaca.Services;

public sealed class FakeSymbolReferenceProvider : ISymbolReferenceProvider
{
    public Task<ValidatedSymbolResult> ValidateSymbolAsync(ValidateSymbolRequest request, CancellationToken cancellationToken)
    {
        var normalized = request.Symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Task.FromResult(ValidatedSymbolResult.Invalid("invalid_symbol", "fake"));
        }

        return Task.FromResult(ValidatedSymbolResult.Valid(normalized, request.AssetClass, "fake", normalized));
    }
}
