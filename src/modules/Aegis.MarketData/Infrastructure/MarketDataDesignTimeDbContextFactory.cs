using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aegis.MarketData.Infrastructure;

public sealed class MarketDataDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MarketDataDbContext>
{
    public MarketDataDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MarketDataDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=aegis;Username=postgres;Password=postgres",
            npgsql => npgsql.UseNodaTime());

        return new MarketDataDbContext(optionsBuilder.Options);
    }
}
