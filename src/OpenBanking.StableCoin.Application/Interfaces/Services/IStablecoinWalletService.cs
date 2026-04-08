using OpenBanking.StableCoin.Application.Common;
using OpenBanking.StableCoin.Application.DTOs.Wallet;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.Interfaces.Services;

public interface IStablecoinWalletService
{
    Task<ServiceResult<WalletBalanceResponse>> GetBalanceAsync(
        string customerId, CancellationToken ct = default);

    Task<ServiceResult<DepositAddressResponse>> GetDepositAddressAsync(
        string customerId, SupportedNetwork network, CancellationToken ct = default);

    Task<ServiceResult<TransferResponse>> SendAsync(
        string customerId, TransferRequest request, CancellationToken ct = default);

    Task<ServiceResult<PagedResult<TransactionHistoryResponse>>> GetTransactionHistoryAsync(
        string customerId, PaginationRequest request, CancellationToken ct = default);
}
