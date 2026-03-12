using Aegis.MarketData.Application.Abstractions;
using Aegis.Universe.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Backend.MarketData;

public sealed class UniverseMarketDataDemandReader(UniverseDbContext dbContext) : IMarketDataSymbolDemandReader
{
    private static readonly string[] DailyCoreProfile = ["daily_core"];

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
}
