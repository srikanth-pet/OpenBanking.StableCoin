using OpenBanking.StableCoin.Application.Models.Coinbase;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.Interfaces.ExternalClients;

public interface ICoinbaseCDPWalletClient
{
    Task<CbWalletResponse> CreateWalletAsync(string customerId, CancellationToken ct = default);

    Task<CbAddressResponse> GetOrCreateDepositAddressAsync(
        string walletId, SupportedNetwork network, CancellationToken ct = default);

    Task<CbTransferResponse> TransferAsync(
        string walletId, CbTransferRequest request, CancellationToken ct = default);

    Task<CbBalanceResponse> GetBalanceAsync(
        string walletId, string assetId, CancellationToken ct = default);

    Task<CbTransferListResponse> ListTransfersAsync(
        string walletId, int pageSize = 20, string? cursor = null, CancellationToken ct = default);
}
