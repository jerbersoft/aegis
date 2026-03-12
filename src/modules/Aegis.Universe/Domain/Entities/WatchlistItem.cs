using NodaTime;

namespace Aegis.Universe.Domain.Entities;

public sealed class WatchlistItem
{
    public Guid WatchlistItemId { get; set; }

    public Guid WatchlistId { get; set; }

    public Guid SymbolId { get; set; }

    public Instant AddedUtc { get; set; }

    public Watchlist Watchlist { get; set; } = null!;

    public Symbol Symbol { get; set; } = null!;
}
