using NodaTime;

namespace Aegis.Universe.Domain.Entities;

public sealed class Symbol
{
    public Guid SymbolId { get; set; }

    public string Ticker { get; set; } = string.Empty;

    public string AssetClass { get; set; } = "us_equities";

    public bool IsActive { get; set; } = true;

    public Instant CreatedUtc { get; set; }

    public Instant UpdatedUtc { get; set; }

    public ICollection<WatchlistItem> WatchlistItems { get; set; } = new List<WatchlistItem>();
}
