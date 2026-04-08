using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.DTOs.Trading;

public sealed record PlaceOrderRequest(
    string ProductId,
    OrderSide Side,
    decimal QuoteSize,
    decimal? LimitPrice = null,
    string? IdempotencyKey = null);

public sealed record PlaceOrderResponse(
    Guid InternalOrderId,
    string? CoinbaseOrderId,
    string ProductId,
    OrderSide Side,
    OrderStatus Status,
    decimal RequestedAmount,
    decimal? FilledAmount,
    decimal? FilledValue,
    decimal? TotalFees,
    DateTimeOffset CreatedAt);
