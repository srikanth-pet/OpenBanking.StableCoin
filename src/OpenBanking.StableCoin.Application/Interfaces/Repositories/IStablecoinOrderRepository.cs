using OpenBanking.StableCoin.Domain.Entities;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.Interfaces.Repositories;

public interface IStablecoinOrderRepository
{
    Task<StablecoinOrder?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StablecoinOrder?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<StablecoinOrder?> GetByCoinbaseOrderIdAsync(string coinbaseOrderId, CancellationToken ct = default);

    Task<(IReadOnlyList<StablecoinOrder> Items, int TotalCount, string? NextCursor)> ListByCustomerAsync(
        string customerId,
        OrderSide? side,
        OrderStatus? status,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int pageSize,
        string? cursor,
        CancellationToken ct = default);

    Task AddAsync(StablecoinOrder order, CancellationToken ct = default);
    Task UpdateAsync(StablecoinOrder order, CancellationToken ct = default);
}
