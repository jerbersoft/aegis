namespace Aegis.Adapters.Alpaca.Configuration;

public sealed class AlpacaSymbolReferenceOptions
{
    public const string SectionName = "Alpaca:SymbolReference";

    public bool UseFakeProvider { get; init; }

    public string BaseUrl { get; init; } = "https://paper-api.alpaca.markets/";

    public string? ApiKey { get; init; }

    public string? ApiSecret { get; init; }

    public int TimeoutSeconds { get; init; } = 10;
}
