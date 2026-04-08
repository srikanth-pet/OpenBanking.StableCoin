using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.DTOs.Trading;

public sealed record OrderPreviewRequest(
    string ProductId,
    OrderSide Side,
    decimal QuoteSize,
    decimal? LimitPrice = null);

public sealed record OrderPreviewResponse(
    decimal BaseSize,
    decimal QuoteSize,
    decimal CommissionTotal,
    decimal BestBidAskPrice,
    decimal AverageFilledPrice,
    string OrderType,
    bool SlippageWarning,
    decimal? SlippagePercentage);
