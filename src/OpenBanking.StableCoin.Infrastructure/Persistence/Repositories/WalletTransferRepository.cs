using Microsoft.EntityFrameworkCore;
using OpenBanking.StableCoin.Application.Interfaces.Repositories;
using OpenBanking.StableCoin.Domain.Entities;

namespace OpenBanking.StableCoin.Infrastructure.Persistence.Repositories;

public sealed class WalletTransferRepository : IWalletTransferRepository
{
    private readonly StablecoinDbContext _db;

    public WalletTransferRepository(StablecoinDbContext db) => _db = db;

    public Task<WalletTransfer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.WalletTransfers.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<WalletTransfer?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        _db.WalletTransfers.FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);

    public Task<WalletTransfer?> GetByCoinbaseTransferIdAsync(string coinbaseTransferId, CancellationToken ct = default) =>
        _db.WalletTransfers.FirstOrDefaultAsync(t => t.CoinbaseTransferId == coinbaseTransferId, ct);

    public async Task<(IReadOnlyList<WalletTransfer> Items, int TotalCount, string? NextCursor)> ListByCustomerAsync(
        string customerId, int pageSize, string? cursor, CancellationToken ct = default)
    {
        var query = _db.WalletTransfers.Where(t => t.CustomerId == customerId);

        if (cursor != null && DateTimeOffset.TryParse(cursor, out var cursorDate))
            query = query.Where(t => t.CreatedAt < cursorDate);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > pageSize)
        {
            items.RemoveAt(pageSize);
            nextCursor = items.Last().CreatedAt.ToString("O");
        }

        return (items, totalCount, nextCursor);
    }

    public async Task AddAsync(WalletTransfer transfer, CancellationToken ct = default)
    {
        _db.WalletTransfers.Add(transfer);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WalletTransfer transfer, CancellationToken ct = default)
    {
        _db.WalletTransfers.Update(transfer);
        await _db.SaveChangesAsync(ct);
    }
}
