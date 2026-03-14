namespace Aegis.Adapters.Alpaca.Configuration;

public sealed class AlpacaRealtimeOptions
{
    public const string SectionName = "Alpaca:Realtime";

    public string? ApiKey { get; init; }

    public string? ApiSecret { get; init; }

    public string Environment { get; init; } = "paper";

    public string Feed { get; init; } = "iex";

    public int ConnectTimeoutSeconds { get; init; } = 15;

    public int EventBufferCapacity { get; init; } = 50_000;

    public int ReconnectInitialDelaySeconds { get; init; } = 1;

    public int ReconnectMaxDelaySeconds { get; init; } = 30;
}
