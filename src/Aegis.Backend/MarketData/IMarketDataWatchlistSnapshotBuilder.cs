using Aegis.Shared.Contracts.MarketData;

namespace Aegis.Backend.MarketData;

public interface IMarketDataWatchlistSnapshotBuilder
{
    Task<MarketDataWatchlistSnapshotEvent?> BuildAsync(Guid watchlistId, long batchSequence, CancellationToken cancellationToken);
}
