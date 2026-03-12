using Microsoft.EntityFrameworkCore;

namespace Aegis.MarketData.Infrastructure;

public static class MarketDataDbInitializer
{
    public static async Task EnsureInitializedAsync(MarketDataDbContext dbContext, CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
    }
}
