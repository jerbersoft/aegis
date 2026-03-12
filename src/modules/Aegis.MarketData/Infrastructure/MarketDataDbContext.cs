using Aegis.MarketData.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aegis.MarketData.Infrastructure;

public sealed class MarketDataDbContext(DbContextOptions<MarketDataDbContext> options) : DbContext(options)
{
    public DbSet<MarketDataBar> Bars => Set<MarketDataBar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MarketDataBar>(entity =>
        {
            entity.ToTable("bar");
            entity.HasKey(x => x.BarId);
            entity.Property(x => x.BarId).HasColumnName("bar_id");
            entity.Property(x => x.Symbol).HasColumnName("symbol").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Interval).HasColumnName("interval").HasMaxLength(16).IsRequired();
            entity.Property(x => x.BarTimeUtc).HasColumnName("bar_time_utc").IsRequired();
            entity.Property(x => x.Open).HasColumnName("open").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.High).HasColumnName("high").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.Low).HasColumnName("low").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.Close).HasColumnName("close").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.Volume).HasColumnName("volume").IsRequired();
            entity.Property(x => x.SessionType).HasColumnName("session_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.MarketDate).HasColumnName("market_date").IsRequired();
            entity.Property(x => x.ProviderName).HasColumnName("provider_name").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ProviderFeed).HasColumnName("provider_feed").HasMaxLength(32).IsRequired();
            entity.Property(x => x.RuntimeState).HasColumnName("runtime_state").HasMaxLength(32).IsRequired();
            entity.Property(x => x.IsReconciled).HasColumnName("is_reconciled").IsRequired();
            entity.Property(x => x.CreatedUtc).HasColumnName("created_utc").IsRequired();
            entity.Property(x => x.UpdatedUtc).HasColumnName("updated_utc").IsRequired();

            entity.HasIndex(x => new { x.Symbol, x.Interval, x.BarTimeUtc }).IsUnique();
            entity.HasIndex(x => new { x.Symbol, x.Interval });
        });
    }
}
