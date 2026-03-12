using Aegis.Shared.Contracts.Universe;
using Aegis.Shared.Enums;
using Aegis.Universe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Universe.Infrastructure;

public sealed class UniverseDbContext(DbContextOptions<UniverseDbContext> options) : DbContext(options)
{
    public DbSet<Symbol> Symbols => Set<Symbol>();

    public DbSet<Watchlist> Watchlists => Set<Watchlist>();

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Symbol>(entity =>
        {
            entity.ToTable("symbol");
            entity.HasKey(x => x.SymbolId);
            entity.Property(x => x.SymbolId).HasColumnName("symbol_id");
            entity.Property(x => x.Ticker).HasColumnName("ticker").HasMaxLength(32).IsRequired();
            entity.Property(x => x.AssetClass).HasColumnName("asset_class").HasMaxLength(64).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(x => x.CreatedUtc).HasColumnName("created_utc").IsRequired();
            entity.Property(x => x.UpdatedUtc).HasColumnName("updated_utc").IsRequired();
            entity.HasIndex(x => x.Ticker).IsUnique();
        });

        modelBuilder.Entity<Watchlist>(entity =>
        {
            entity.ToTable("watchlist");
            entity.HasKey(x => x.WatchlistId);
            entity.Property(x => x.WatchlistId).HasColumnName("watchlist_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
            entity.Property(x => x.NormalizedName).HasColumnName("normalized_name").HasMaxLength(128).IsRequired();
            entity.Property(x => x.WatchlistType)
                .HasColumnName("watchlist_type")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(x => x.IsSystem).HasColumnName("is_system").IsRequired();
            entity.Property(x => x.IsMutable).HasColumnName("is_mutable").IsRequired();
            entity.Property(x => x.CreatedUtc).HasColumnName("created_utc").IsRequired();
            entity.Property(x => x.UpdatedUtc).HasColumnName("updated_utc").IsRequired();
            entity.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<WatchlistItem>(entity =>
        {
            entity.ToTable("watchlist_item");
            entity.HasKey(x => x.WatchlistItemId);
            entity.Property(x => x.WatchlistItemId).HasColumnName("watchlist_item_id");
            entity.Property(x => x.WatchlistId).HasColumnName("watchlist_id").IsRequired();
            entity.Property(x => x.SymbolId).HasColumnName("symbol_id").IsRequired();
            entity.Property(x => x.AddedUtc).HasColumnName("added_utc").IsRequired();

            entity.HasOne(x => x.Watchlist)
                .WithMany(x => x.WatchlistItems)
                .HasForeignKey(x => x.WatchlistId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Symbol)
                .WithMany(x => x.WatchlistItems)
                .HasForeignKey(x => x.SymbolId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.WatchlistId, x.SymbolId }).IsUnique();
            entity.HasIndex(x => x.SymbolId);
        });

    }
}
