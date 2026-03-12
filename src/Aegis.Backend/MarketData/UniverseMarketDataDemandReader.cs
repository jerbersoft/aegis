using Aegis.MarketData.Application.Abstractions;
using Aegis.Shared.Contracts.Universe;
using Aegis.Universe.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Backend.MarketData;

public sealed class UniverseMarketDataDemandReader(UniverseDbContext dbContext) : IMarketDataSymbolDemandReader
{
    private static readonly string[] DailyCoreProfile = ["daily_core"];
    private static readonly string[] IntradayCoreProfile = ["intraday_core"];

    public async Task<IReadOnlyList<DailySymbolDemand>> GetDailyDemandAsync(CancellationToken cancellationToken) =>
        // Daily warmup scope is defined by every active symbol currently present in the Universe registry.
        (await dbContext.Symbols
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Ticker)
            .Select(x => x.Ticker)
            .ToListAsync(cancellationToken))
        .Select(symbol => new DailySymbolDemand(symbol, "watchlist_symbol", DailyCoreProfile))
        .ToArray();

    public async Task<IReadOnlyList<IntradaySymbolDemand>> GetIntradayDemandAsync(CancellationToken cancellationToken) =>
        (await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(x => x.Watchlist.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant())
            .OrderBy(x => x.Symbol.Ticker)
            .Select(x => x.Symbol.Ticker)
            .ToListAsync(cancellationToken))
        .Select(symbol => new IntradaySymbolDemand(symbol, "1min", "execution_symbol", IntradayCoreProfile))
        .ToArray();
}
