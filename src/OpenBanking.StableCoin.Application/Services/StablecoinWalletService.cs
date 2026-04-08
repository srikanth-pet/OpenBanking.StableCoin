using Microsoft.Extensions.Logging;
using OpenBanking.StableCoin.Application.Common;
using OpenBanking.StableCoin.Application.DTOs.Wallet;
using OpenBanking.StableCoin.Application.Exceptions;
using OpenBanking.StableCoin.Application.Interfaces.ExternalClients;
using OpenBanking.StableCoin.Application.Interfaces.Repositories;
using OpenBanking.StableCoin.Application.Interfaces.Services;
using OpenBanking.StableCoin.Application.Models.Coinbase;
using OpenBanking.StableCoin.Domain.Entities;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.Services;

public sealed class StablecoinWalletService : IStablecoinWalletService
{
    private readonly ICoinbaseCDPWalletClient _cdpClient;
    private readonly ICustomerWalletRepository _walletRepo;
    private readonly IWalletTransferRepository _transferRepo;
    private readonly ILogger<StablecoinWalletService> _logger;

    private static readonly Dictionary<SupportedNetwork, string> NetworkDisplayNames = new()
    {
        [SupportedNetwork.Base] = "Base",
        [SupportedNetwork.Ethereum] = "Ethereum",
        [SupportedNetwork.Polygon] = "Polygon"
    };

    public StablecoinWalletService(
        ICoinbaseCDPWalletClient cdpClient,
        ICustomerWalletRepository walletRepo,
        IWalletTransferRepository transferRepo,
        ILogger<StablecoinWalletService> logger)
    {
        _cdpClient = cdpClient;
        _walletRepo = walletRepo;
        _transferRepo = transferRepo;
        _logger = logger;
    }

    public async Task<ServiceResult<WalletBalanceResponse>> GetBalanceAsync(
        string customerId, CancellationToken ct = default)
    {
        _logger.LogDebug("GetBalance: CustomerId={CustomerId}", customerId);

        var wallet = await _walletRepo.GetByCustomerIdAsync(customerId, ct);
        if (wallet == null)
        {
            _logger.LogWarning("GetBalance: wallet not found for CustomerId={CustomerId}", customerId);
            return ServiceResult<WalletBalanceResponse>.NotFound(
                "No wallet found for customer.", "WALLET_NOT_FOUND");
        }

        try
        {
            var balance = await _cdpClient.GetBalanceAsync(wallet.CoinbaseWalletId, "USDC", ct);
            var amount = decimal.TryParse(balance.Data?.Amount, out var a) ? a : 0m;

            _logger.LogDebug("Balance fetched: CustomerId={CustomerId} Amount={Amount} USDC", customerId, amount);

            return ServiceResult<WalletBalanceResponse>.Success(new WalletBalanceResponse(
                AssetId: "USDC",
                AvailableBalance: amount,
                TotalBalance: amount,
                Currency: "USD",
                BalanceByNetwork: new Dictionary<SupportedNetwork, decimal>
                {
                    [SupportedNetwork.Base] = amount
                }));
        }
        catch (CoinbaseApiException ex)
        {
            _logger.LogError(ex, "Failed to fetch balance for CustomerId={CustomerId}", customerId);
            return ServiceResult<WalletBalanceResponse>.Failure(ex.ErrorCode, ex.Message, ex.HttpStatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching balance for CustomerId={CustomerId}", customerId);
            throw;
        }
    }

    public async Task<ServiceResult<DepositAddressResponse>> GetDepositAddressAsync(
        string customerId, SupportedNetwork network, CancellationToken ct = default)
    {
        var wallet = await _walletRepo.GetByCustomerIdAsync(customerId, ct);
        if (wallet == null)
            return ServiceResult<DepositAddressResponse>.NotFound(
                "No wallet found for customer.", "WALLET_NOT_FOUND");

        // Check cached address
        var cachedAddress = wallet.GetDepositAddress(network);
        if (cachedAddress != null)
        {
            return ServiceResult<DepositAddressResponse>.Success(new DepositAddressResponse(
                Address: cachedAddress,
                Network: network,
                NetworkDisplayName: NetworkDisplayNames[network],
                AssetId: "USDC",
                QrCodeBase64: null));
        }

        // Fetch from Coinbase and cache
        try
        {
            var addressResponse = await _cdpClient.GetOrCreateDepositAddressAsync(
                wallet.CoinbaseWalletId, network, ct);
            var address = addressResponse.AddressInfo?.Address ?? addressResponse.Address;

            wallet.AddDepositAddress(network, address);
            await _walletRepo.UpdateAsync(wallet, ct);

            return ServiceResult<DepositAddressResponse>.Success(new DepositAddressResponse(
                Address: address,
                Network: network,
                NetworkDisplayName: NetworkDisplayNames[network],
                AssetId: "USDC",
                QrCodeBase64: null));
        }
        catch (CoinbaseApiException ex)
        {
            _logger.LogError(ex, "Failed to get deposit address for CustomerId {CustomerId}", customerId);
            return ServiceResult<DepositAddressResponse>.Failure(ex.ErrorCode, ex.Message, ex.HttpStatusCode);
        }
    }

    public async Task<ServiceResult<TransferResponse>> SendAsync(
        string customerId, TransferRequest request, CancellationToken ct = default)
    {
        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();

        // Idempotency check
        var existing = await _transferRepo.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing != null)
        {
            _logger.LogInformation("Idempotent replay for transfer key {Key}", idempotencyKey);
            return ServiceResult<TransferResponse>.Success(MapToTransferResponse(existing));
        }

        var wallet = await _walletRepo.GetByCustomerIdAsync(customerId, ct);
        if (wallet == null)
            return ServiceResult<TransferResponse>.NotFound(
                "No wallet found for customer.", "WALLET_NOT_FOUND");

        // Persist local record first
        var transfer = WalletTransfer.Create(
            customerId, wallet.CoinbaseWalletId, request.ToAddress,
            request.Amount, request.AssetId.ToUpperInvariant(),
            request.Network, idempotencyKey);
        await _transferRepo.AddAsync(transfer, ct);

        try
        {
            var cbRequest = new CbTransferRequest
            {
                To = request.ToAddress,
                Amount = request.Amount.ToString("F8"),
                Currency = request.AssetId.ToUpperInvariant(),
                Type = "send",
                IdempotencyKey = idempotencyKey,
                Description = request.Memo
            };

            var result = await _cdpClient.TransferAsync(wallet.CoinbaseWalletId, cbRequest, ct);

            if (result.Data != null)
            {
                transfer.MarkBroadcast(result.Data.Id);
                if (result.Data.Network?.Hash != null)
                    transfer.MarkConfirmed(result.Data.Network.Hash);
            }
        }
        catch (CoinbaseApiException ex)
        {
            _logger.LogError(ex, "Failed to send USDC for CustomerId {CustomerId}", customerId);
            transfer.MarkFailed(ex.Message);
            await _transferRepo.UpdateAsync(transfer, ct);
            return ServiceResult<TransferResponse>.Failure(ex.ErrorCode, ex.Message, ex.HttpStatusCode);
        }

        await _transferRepo.UpdateAsync(transfer, ct);
        return ServiceResult<TransferResponse>.Success(MapToTransferResponse(transfer));
    }

    public async Task<ServiceResult<PagedResult<TransactionHistoryResponse>>> GetTransactionHistoryAsync(
        string customerId, PaginationRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug("GetTransactionHistory: CustomerId={CustomerId} PageSize={PageSize}", customerId, request.PageSize);

        var (items, totalCount, nextCursor) = await _transferRepo.ListByCustomerAsync(
            customerId, request.PageSize, request.Cursor, ct);

        _logger.LogDebug("GetTransactionHistory returned {Count} of {Total} for CustomerId={CustomerId}", items.Count(), totalCount, customerId);

        return ServiceResult<PagedResult<TransactionHistoryResponse>>.Success(
            new PagedResult<TransactionHistoryResponse>
            {
                Items = items.Select(MapToHistoryResponse).ToList(),
                TotalCount = totalCount,
                PageSize = request.PageSize,
                NextCursor = nextCursor
            });
    }

    private static TransferResponse MapToTransferResponse(WalletTransfer t) =>
        new(t.Id, t.CoinbaseTransferId, t.ToAddress, t.Amount, t.AssetId,
            t.Network, t.Status, t.TransactionHash, t.CreatedAt);

    private static TransactionHistoryResponse MapToHistoryResponse(WalletTransfer t) =>
        new(t.Id, t.CoinbaseTransferId, t.ToAddress, t.Amount, t.AssetId,
            t.Network, t.Status, t.TransactionHash, t.FailureReason,
            t.CreatedAt, t.ConfirmedAt);
}
