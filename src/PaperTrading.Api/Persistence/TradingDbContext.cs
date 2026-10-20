using Microsoft.EntityFrameworkCore;
using PaperTrading.Domain;

namespace PaperTrading.Api.Persistence;

/// <summary>
/// Maps the domain objects directly (private setters and backing fields), so there is no
/// second copy of the model to keep in sync. SQLite stores decimals as TEXT, which keeps them exact.
/// </summary>
public sealed class TradingDbContext(DbContextOptions<TradingDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Account>(account =>
        {
            account.HasKey(a => a.Id);
            account.HasMany(a => a.Positions).WithOne().HasForeignKey(p => p.AccountId);
            account.Navigation(a => a.Positions).HasField("_positions").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Position>(position =>
        {
            position.HasKey(p => new { p.AccountId, p.Symbol });
            position.Property(p => p.Symbol).HasMaxLength(20);
        });

        modelBuilder.Entity<Order>(order =>
        {
            order.HasKey(o => o.Id);
            order.HasOne<Account>().WithMany().HasForeignKey(o => o.AccountId);
            order.Property(o => o.Symbol).HasMaxLength(20);
            order.Property(o => o.IdempotencyKey).HasMaxLength(MatchingEngine.MaxIdempotencyKeyLength);
            order.Property(o => o.Side).HasConversion<string>().HasMaxLength(10);
            order.Property(o => o.Type).HasConversion<string>().HasMaxLength(10);
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(10);
            order.Property(o => o.RejectReason).HasMaxLength(40);

            // Safety net behind the engine's own idempotency check.
            order.HasIndex(o => new { o.AccountId, o.IdempotencyKey }).IsUnique();
            order.HasIndex(o => new { o.AccountId, o.Sequence });
        });
    }
}
