using OpenBanking.StableCoin.Domain.Entities;

namespace OpenBanking.StableCoin.Application.Interfaces.Repositories;

public interface ICustomerWalletRepository
{
    Task<CustomerWallet?> GetByCustomerIdAsync(string customerId, CancellationToken ct = default);
    Task AddAsync(CustomerWallet wallet, CancellationToken ct = default);
    Task UpdateAsync(CustomerWallet wallet, CancellationToken ct = default);
}
