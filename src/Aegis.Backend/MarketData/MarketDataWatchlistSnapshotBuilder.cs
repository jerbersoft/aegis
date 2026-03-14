using Aegis.MarketData.Application;
using Aegis.Shared.Contracts.MarketData;
using Aegis.Universe.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Aegis.Backend.MarketData;

public sealed class MarketDataWatchlistSnapshotBuilder(
    IServiceScopeFactory scopeFactory,
    MarketDataDailyRuntimeStore dailyRuntimeStore,
    IClock clock) : IMarketDataWatchlistSnapshotBuilder
{
    public async Task<MarketDataWatchlistSnapshotEvent?> BuildAsync(Guid watchlistId, long batchSequence, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var universeDbContext = scope.ServiceProvider.GetRequiredService<UniverseDbContext>();

        var watchlistExists = await universeDbContext.Watchlists
            .AsNoTracking()
            .AnyAsync(x => x.WatchlistId == watchlistId, cancellationToken);

        if (!watchlistExists)
        {
            return null;
        }

        var tickers = await universeDbContext.WatchlistItems
            .AsNoTracking()
            .Where(x => x.WatchlistId == watchlistId)
            .Select(x => x.Symbol.Ticker)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var asOfUtc = clock.GetCurrentInstant();
        var dailySnapshot = dailyRuntimeStore.GetSnapshot();
        var previousCloseBySymbol = dailySnapshot.Symbols
            .Where(x => x.Bars.Count >= 2)
            .ToDictionary(x => x.Symbol, x => x.Bars[^2].Close, StringComparer.OrdinalIgnoreCase);

        var symbols = tickers
            .Select(ticker =>
            {
                var runtime = dailyRuntimeStore.GetSymbol(ticker);
                var currentPrice = runtime?.Bars.LastOrDefault()?.Close;
                decimal? percentChange = null;

                if (currentPrice.HasValue && previousCloseBySymbol.TryGetValue(ticker, out var previousClose) && previousClose != 0)
                {
                    percentChange = ((currentPrice.Value / previousClose) - 1m) * 100m;
                }

                return new MarketDataWatchlistSymbolSnapshot(ticker, currentPrice, percentChange);
            })
            .ToList();

        return new MarketDataWatchlistSnapshotEvent(
            MarketDataRealtimeContract.ContractVersion,
            Guid.NewGuid().ToString("N"),
            watchlistId,
            batchSequence,
            asOfUtc,
            asOfUtc,
            true,
            symbols);
    }
}
