namespace Aegis.Shared.Ports.MarketData;

public sealed record ValidateSymbolRequest(string Symbol, string AssetClass = "us_equities");

public sealed record ValidatedSymbolResult(
    bool IsValid,
    string? NormalizedSymbol,
    string AssetClass,
    string ProviderName,
    string? DisplayName,
    string? Exchange,
    string ReasonCode)
{
    public static ValidatedSymbolResult Valid(
        string normalizedSymbol,
        string assetClass,
        string providerName,
        string? displayName = null,
        string? exchange = null) =>
        new(true, normalizedSymbol, assetClass, providerName, displayName, exchange, "none");

    public static ValidatedSymbolResult Invalid(string reasonCode, string providerName) =>
        new(false, null, "us_equities", providerName, null, null, reasonCode);
}

public interface ISymbolReferenceProvider
{
    Task<ValidatedSymbolResult> ValidateSymbolAsync(ValidateSymbolRequest request, CancellationToken cancellationToken);
}
