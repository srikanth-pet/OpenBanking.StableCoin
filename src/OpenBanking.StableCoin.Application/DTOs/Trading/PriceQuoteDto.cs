using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.DTOs.Trading;

public sealed record PriceQuoteRequest(
    string ProductId,
    OrderSide Side,
    decimal Amount);

public sealed record PriceQuoteResponse(
    string ProductId,
    decimal BestBidPrice,
    decimal BestAskPrice,
    decimal EffectivePrice,
    decimal Amount,
    string Currency,
    DateTimeOffset QuoteTime,
    DateTimeOffset ExpiresAt);
