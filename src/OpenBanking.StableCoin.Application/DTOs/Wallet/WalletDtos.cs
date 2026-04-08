using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.DTOs.Wallet;

public sealed record WalletBalanceResponse(
    string AssetId,
    decimal AvailableBalance,
    decimal TotalBalance,
    string Currency,
    Dictionary<SupportedNetwork, decimal> BalanceByNetwork);

public sealed record DepositAddressResponse(
    string Address,
    SupportedNetwork Network,
    string NetworkDisplayName,
    string AssetId,
    string? QrCodeBase64);

public sealed record TransferRequest(
    string ToAddress,
    decimal Amount,
    string AssetId,
    SupportedNetwork Network,
    string? IdempotencyKey = null,
    string? Memo = null);

public sealed record TransferResponse(
    Guid InternalTransferId,
    string? CoinbaseTransferId,
    string ToAddress,
    decimal Amount,
    string AssetId,
    SupportedNetwork Network,
    TransferStatus Status,
    string? TransactionHash,
    DateTimeOffset CreatedAt);

public sealed record TransactionHistoryResponse(
    Guid InternalTransferId,
    string? CoinbaseTransferId,
    string ToAddress,
    decimal Amount,
    string AssetId,
    SupportedNetwork Network,
    TransferStatus Status,
    string? TransactionHash,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt);
