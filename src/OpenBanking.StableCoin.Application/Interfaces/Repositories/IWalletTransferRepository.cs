using OpenBanking.StableCoin.Domain.Entities;

namespace OpenBanking.StableCoin.Application.Interfaces.Repositories;

public interface IWalletTransferRepository
{
    Task<WalletTransfer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WalletTransfer?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<WalletTransfer?> GetByCoinbaseTransferIdAsync(string coinbaseTransferId, CancellationToken ct = default);

    Task<(IReadOnlyList<WalletTransfer> Items, int TotalCount, string? NextCursor)> ListByCustomerAsync(
        string customerId, int pageSize, string? cursor, CancellationToken ct = default);

    Task AddAsync(WalletTransfer transfer, CancellationToken ct = default);
    Task UpdateAsync(WalletTransfer transfer, CancellationToken ct = default);
}
