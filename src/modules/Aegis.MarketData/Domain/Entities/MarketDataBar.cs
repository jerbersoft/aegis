namespace Aegis.MarketData.Domain.Entities;

public sealed class MarketDataBar
{
    public Guid BarId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string Interval { get; set; } = string.Empty;

    public DateTimeOffset BarTimeUtc { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public long Volume { get; set; }

    public string SessionType { get; set; } = string.Empty;

    public DateOnly MarketDate { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderFeed { get; set; } = string.Empty;

    public string RuntimeState { get; set; } = string.Empty;

    public bool IsReconciled { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }
}
