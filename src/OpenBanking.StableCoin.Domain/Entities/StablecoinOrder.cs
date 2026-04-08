using OpenBanking.StableCoin.Domain.Common;
using OpenBanking.StableCoin.Domain.Enums;
using OpenBanking.StableCoin.Domain.Exceptions;

namespace OpenBanking.StableCoin.Domain.Entities;

public class StablecoinOrder : AuditableEntity
{
    public string CustomerId { get; private set; } = string.Empty;
    public string? CoinbaseOrderId { get; private set; }
    public string ClientOrderId { get; private set; } = string.Empty;
    public string ProductId { get; private set; } = string.Empty;
    public OrderSide Side { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public decimal? FilledAmount { get; private set; }
    public decimal? FilledValue { get; private set; }
    public decimal? TotalFees { get; private set; }
    public decimal? AverageFilledPrice { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? RawCoinbaseResponse { get; private set; }

    private StablecoinOrder() { }

    public static StablecoinOrder Create(
        string customerId,
        string productId,
        OrderSide side,
        decimal requestedAmount,
        string clientOrderId,
        string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new DomainException("CustomerId is required.", "INVALID_CUSTOMER_ID");
        if (string.IsNullOrWhiteSpace(productId))
            throw new DomainException("ProductId is required.", "INVALID_PRODUCT_ID");
        if (requestedAmount <= 0)
            throw new DomainException("Amount must be greater than zero.", "INVALID_AMOUNT");

        return new StablecoinOrder
        {
            CustomerId = customerId,
            ProductId = productId,
            Side = side,
            RequestedAmount = requestedAmount,
            ClientOrderId = clientOrderId,
            IdempotencyKey = idempotencyKey,
            Status = OrderStatus.Pending,
            CreatedBy = customerId
        };
    }

    public void UpdateStatus(
        OrderStatus status,
        string? coinbaseOrderId = null,
        decimal? filledAmount = null,
        decimal? filledValue = null,
        decimal? totalFees = null,
        decimal? averageFilledPrice = null,
        string? failureReason = null,
        string? rawResponse = null)
    {
        Status = status;
        if (coinbaseOrderId != null) CoinbaseOrderId = coinbaseOrderId;
        if (filledAmount.HasValue) FilledAmount = filledAmount;
        if (filledValue.HasValue) FilledValue = filledValue;
        if (totalFees.HasValue) TotalFees = totalFees;
        if (averageFilledPrice.HasValue) AverageFilledPrice = averageFilledPrice;
        if (failureReason != null) FailureReason = failureReason;
        if (rawResponse != null) RawCoinbaseResponse = rawResponse;
        UpdatedAt = DateTimeOffset.UtcNow;

        if (status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Failed or OrderStatus.Expired)
            CompletedAt = DateTimeOffset.UtcNow;
    }

    public bool IsTerminal =>
        Status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Failed or OrderStatus.Expired;
}
