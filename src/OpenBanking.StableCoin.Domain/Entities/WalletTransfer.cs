using OpenBanking.StableCoin.Domain.Common;
using OpenBanking.StableCoin.Domain.Enums;
using OpenBanking.StableCoin.Domain.Exceptions;

namespace OpenBanking.StableCoin.Domain.Entities;

public class WalletTransfer : AuditableEntity
{
    public string CustomerId { get; private set; } = string.Empty;
    public string CoinbaseWalletId { get; private set; } = string.Empty;
    public string ToAddress { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string AssetId { get; private set; } = string.Empty;
    public SupportedNetwork Network { get; private set; }
    public TransferStatus Status { get; private set; }
    public string? CoinbaseTransferId { get; private set; }
    public string? TransactionHash { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }

    private WalletTransfer() { }

    public static WalletTransfer Create(
        string customerId,
        string coinbaseWalletId,
        string toAddress,
        decimal amount,
        string assetId,
        SupportedNetwork network,
        string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new DomainException("CustomerId is required.", "INVALID_CUSTOMER_ID");
        if (string.IsNullOrWhiteSpace(toAddress))
            throw new DomainException("ToAddress is required.", "INVALID_ADDRESS");
        if (amount <= 0)
            throw new DomainException("Amount must be greater than zero.", "INVALID_AMOUNT");

        return new WalletTransfer
        {
            CustomerId = customerId,
            CoinbaseWalletId = coinbaseWalletId,
            ToAddress = toAddress,
            Amount = amount,
            AssetId = assetId,
            Network = network,
            IdempotencyKey = idempotencyKey,
            Status = TransferStatus.Pending,
            CreatedBy = customerId
        };
    }

    public void MarkBroadcast(string coinbaseTransferId)
    {
        Status = TransferStatus.Broadcast;
        CoinbaseTransferId = coinbaseTransferId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkConfirmed(string transactionHash)
    {
        Status = TransferStatus.Confirmed;
        TransactionHash = transactionHash;
        ConfirmedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = TransferStatus.Failed;
        FailureReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
