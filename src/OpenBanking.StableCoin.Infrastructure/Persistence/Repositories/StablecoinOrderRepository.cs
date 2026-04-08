using Microsoft.EntityFrameworkCore;
using OpenBanking.StableCoin.Application.Interfaces.Repositories;
using OpenBanking.StableCoin.Domain.Entities;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Infrastructure.Persistence.Repositories;

public sealed class StablecoinOrderRepository : IStablecoinOrderRepository
{
    private readonly StablecoinDbContext _db;

    public StablecoinOrderRepository(StablecoinDbContext db) => _db = db;

    public Task<StablecoinOrder?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.StablecoinOrders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<StablecoinOrder?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        _db.StablecoinOrders.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);

    public Task<StablecoinOrder?> GetByCoinbaseOrderIdAsync(string coinbaseOrderId, CancellationToken ct = default) =>
        _db.StablecoinOrders.FirstOrDefaultAsync(o => o.CoinbaseOrderId == coinbaseOrderId, ct);

    public async Task<(IReadOnlyList<StablecoinOrder> Items, int TotalCount, string? NextCursor)> ListByCustomerAsync(
        string customerId,
        OrderSide? side,
        OrderStatus? status,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int pageSize,
        string? cursor,
        CancellationToken ct = default)
    {
        var query = _db.StablecoinOrders
            .Where(o => o.CustomerId == customerId);

        if (side.HasValue) query = query.Where(o => o.Side == side.Value);
        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (fromDate.HasValue) query = query.Where(o => o.CreatedAt >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(o => o.CreatedAt <= toDate.Value);

        // Cursor-based pagination using CreatedAt
        if (cursor != null && DateTimeOffset.TryParse(cursor, out var cursorDate))
            query = query.Where(o => o.CreatedAt < cursorDate);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
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

    public async Task AddAsync(StablecoinOrder order, CancellationToken ct = default)
    {
        _db.StablecoinOrders.Add(order);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(StablecoinOrder order, CancellationToken ct = default)
    {
        _db.StablecoinOrders.Update(order);
        await _db.SaveChangesAsync(ct);
    }
}
