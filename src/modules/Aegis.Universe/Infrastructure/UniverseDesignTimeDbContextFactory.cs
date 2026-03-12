using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aegis.Universe.Infrastructure;

public sealed class UniverseDesignTimeDbContextFactory : IDesignTimeDbContextFactory<UniverseDbContext>
{
    public UniverseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UniverseDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=aegis;Username=postgres;Password=postgres",
            npgsql => npgsql.UseNodaTime());

        return new UniverseDbContext(optionsBuilder.Options);
    }
}
