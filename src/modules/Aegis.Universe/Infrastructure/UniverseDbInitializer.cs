using Aegis.Shared.Contracts.Universe;
using Aegis.Shared.Enums;
using Aegis.Universe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Aegis.Universe.Infrastructure;

public static class UniverseDbInitializer
{
    public static async Task EnsureInitializedAsync(UniverseDbContext dbContext, CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        var normalizedExecution = WatchlistConventions.ExecutionName.ToUpperInvariant();
        var executionExists = await dbContext.Watchlists.AnyAsync(
            x => x.NormalizedName == normalizedExecution,
            cancellationToken);

        if (executionExists)
        {
            return;
        }

        var now = SystemClock.Instance.GetCurrentInstant();
        dbContext.Watchlists.Add(new Watchlist
        {
            WatchlistId = Guid.NewGuid(),
            Name = WatchlistConventions.ExecutionName,
            NormalizedName = normalizedExecution,
            WatchlistType = WatchlistType.System,
            IsSystem = true,
            IsMutable = false,
            CreatedUtc = now,
            UpdatedUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
