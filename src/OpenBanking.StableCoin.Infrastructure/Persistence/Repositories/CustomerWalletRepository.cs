using Microsoft.EntityFrameworkCore;
using OpenBanking.StableCoin.Application.Interfaces.Repositories;
using OpenBanking.StableCoin.Domain.Entities;

namespace OpenBanking.StableCoin.Infrastructure.Persistence.Repositories;

public sealed class CustomerWalletRepository : ICustomerWalletRepository
{
    private readonly StablecoinDbContext _db;

    public CustomerWalletRepository(StablecoinDbContext db) => _db = db;

    public Task<CustomerWallet?> GetByCustomerIdAsync(string customerId, CancellationToken ct = default) =>
        _db.CustomerWallets.FirstOrDefaultAsync(w => w.CustomerId == customerId && w.IsActive, ct);

    public async Task AddAsync(CustomerWallet wallet, CancellationToken ct = default)
    {
        _db.CustomerWallets.Add(wallet);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CustomerWallet wallet, CancellationToken ct = default)
    {
        _db.CustomerWallets.Update(wallet);
        await _db.SaveChangesAsync(ct);
    }
}
