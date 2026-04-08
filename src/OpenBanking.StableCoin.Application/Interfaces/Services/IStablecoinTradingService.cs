using OpenBanking.StableCoin.Application.Common;
using OpenBanking.StableCoin.Application.DTOs.Trading;

namespace OpenBanking.StableCoin.Application.Interfaces.Services;

public interface IStablecoinTradingService
{
    Task<ServiceResult<PriceQuoteResponse>> GetPriceQuoteAsync(
        string customerId, PriceQuoteRequest request, CancellationToken ct = default);

    Task<ServiceResult<OrderPreviewResponse>> PreviewOrderAsync(
        string customerId, OrderPreviewRequest request, CancellationToken ct = default);

    Task<ServiceResult<PlaceOrderResponse>> PlaceOrderAsync(
        string customerId, PlaceOrderRequest request, CancellationToken ct = default);

    Task<ServiceResult<OrderStatusResponse>> GetOrderStatusAsync(
        string customerId, Guid orderId, CancellationToken ct = default);

    Task<ServiceResult<PagedResult<OrderStatusResponse>>> ListOrdersAsync(
        string customerId, OrderListRequest request, CancellationToken ct = default);
}
