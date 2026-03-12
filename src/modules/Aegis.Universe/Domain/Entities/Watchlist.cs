using Aegis.Shared.Enums;
using NodaTime;

namespace Aegis.Universe.Domain.Entities;

public sealed class Watchlist
{
    public Guid WatchlistId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public WatchlistType WatchlistType { get; set; }

    public bool IsSystem { get; set; }

    public bool IsMutable { get; set; }

    public Instant CreatedUtc { get; set; }

    public Instant UpdatedUtc { get; set; }

    public ICollection<WatchlistItem> WatchlistItems { get; set; } = new List<WatchlistItem>();
}
