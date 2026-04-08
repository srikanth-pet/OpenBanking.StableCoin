using Microsoft.EntityFrameworkCore;
using OpenBanking.StableCoin.Domain.Common;
using OpenBanking.StableCoin.Domain.Entities;

namespace OpenBanking.StableCoin.Infrastructure.Persistence;

public class StablecoinDbContext : DbContext
{
    public StablecoinDbContext(DbContextOptions<StablecoinDbContext> options) : base(options) { }

    public DbSet<StablecoinOrder> StablecoinOrders => Set<StablecoinOrder>();
    public DbSet<WalletTransfer> WalletTransfers => Set<WalletTransfer>();
    public DbSet<CustomerWallet> CustomerWallets => Set<CustomerWallet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("stablecoin");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StablecoinDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>()
                     .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}
