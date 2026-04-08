using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.DTOs.Trading;

public sealed record OrderStatusResponse(
    Guid InternalOrderId,
    string? CoinbaseOrderId,
    string ProductId,
    OrderSide Side,
    OrderStatus Status,
    decimal RequestedAmount,
    decimal? FilledAmount,
    decimal? FilledValue,
    decimal? TotalFees,
    decimal? AverageFilledPrice,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record OrderListRequest(
    OrderSide? Side = null,
    OrderStatus? Status = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int PageSize = 20,
    string? Cursor = null);
