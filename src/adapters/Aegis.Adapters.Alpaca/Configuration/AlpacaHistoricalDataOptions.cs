namespace Aegis.Adapters.Alpaca.Configuration;

public sealed class AlpacaHistoricalDataOptions
{
    public const string SectionName = "Alpaca:HistoricalData";

    public string BaseUrl { get; init; } = "https://data.alpaca.markets/";

    public string? ApiKey { get; init; }

    public string? ApiSecret { get; init; }

    public int TimeoutSeconds { get; init; } = 10;

    public string Feed { get; init; } = "iex";
}
