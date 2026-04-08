using OpenBanking.StableCoin.Application.Models.Coinbase;

namespace OpenBanking.StableCoin.Application.Interfaces.ExternalClients;

public interface ICoinbaseAdvancedTradeClient
{
    Task<CbProductResponse> GetProductAsync(string productId, CancellationToken ct = default);

    Task<CbOrderPreviewResponse> PreviewOrderAsync(CbOrderPreviewRequest request, CancellationToken ct = default);

    Task<CbCreateOrderResponse> CreateOrderAsync(CbCreateOrderRequest request, CancellationToken ct = default);

    Task<CbOrderResponse> GetOrderAsync(string orderId, CancellationToken ct = default);

    Task<CbListOrdersResponse> ListOrdersAsync(CbListOrdersRequest request, CancellationToken ct = default);
}
