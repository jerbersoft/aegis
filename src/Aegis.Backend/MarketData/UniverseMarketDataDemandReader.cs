using Aegis.MarketData.Application.Abstractions;
using Aegis.Universe.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Backend.MarketData;

public sealed class UniverseMarketDataDemandReader(UniverseDbContext dbContext) : IMarketDataSymbolDemandReader
{
    public async Task<IReadOnlyList<string>> GetDailyWarmupSymbolsAsync(CancellationToken cancellationToken) =>
        await dbContext.Symbols
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Ticker)
            .Select(x => x.Ticker)
            .ToListAsync(cancellationToken);
}
