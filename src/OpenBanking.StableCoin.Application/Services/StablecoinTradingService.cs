using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenBanking.StableCoin.Application.Common;
using OpenBanking.StableCoin.Application.DTOs.Trading;
using OpenBanking.StableCoin.Application.Exceptions;
using OpenBanking.StableCoin.Application.Interfaces.ExternalClients;
using OpenBanking.StableCoin.Application.Interfaces.Repositories;
using OpenBanking.StableCoin.Application.Interfaces.Services;
using OpenBanking.StableCoin.Application.Models.Coinbase;
using OpenBanking.StableCoin.Domain.Entities;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Application.Services;

public sealed class StablecoinTradingService : IStablecoinTradingService
{
    private readonly ICoinbaseAdvancedTradeClient _coinbase;
    private readonly IStablecoinOrderRepository _orderRepo;
    private readonly ICustomerWalletRepository _walletRepo;
    private readonly ILogger<StablecoinTradingService> _logger;

    public StablecoinTradingService(
        ICoinbaseAdvancedTradeClient coinbase,
        IStablecoinOrderRepository orderRepo,
        ICustomerWalletRepository walletRepo,
        ILogger<StablecoinTradingService> logger)
    {
        _coinbase = coinbase;
        _orderRepo = orderRepo;
        _walletRepo = walletRepo;
        _logger = logger;
    }

    public async Task<ServiceResult<PriceQuoteResponse>> GetPriceQuoteAsync(
        string customerId, PriceQuoteRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug("GetPriceQuote: CustomerId={CustomerId} ProductId={ProductId} Side={Side}",
            customerId, request.ProductId, request.Side);
        try
        {
            var product = await _coinbase.GetProductAsync(request.ProductId, ct);
            var bestBid = decimal.Parse(product.BestBid);
            var bestAsk = decimal.Parse(product.BestAsk);
            var effectivePrice = request.Side == OrderSide.Buy ? bestAsk : bestBid;

            _logger.LogDebug("Price quote: BestBid={BestBid} BestAsk={BestAsk} Effective={Effective}",
                bestBid, bestAsk, effectivePrice);

            return ServiceResult<PriceQuoteResponse>.Success(new PriceQuoteResponse(
                ProductId: request.ProductId,
                BestBidPrice: bestBid,
                BestAskPrice: bestAsk,
                EffectivePrice: effectivePrice,
                Amount: request.Amount,
                Currency: product.QuoteCurrencyId,
                QuoteTime: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(30)));
        }
        catch (CoinbaseApiException ex)
        {
            _logger.LogWarning(ex, "Coinbase API error getting price quote for {ProductId} CustomerId={CustomerId}",
                request.ProductId, customerId);
            return ServiceResult<PriceQuoteResponse>.Failure(ex.ErrorCode, ex.Message, ex.HttpStatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting price quote for {ProductId} CustomerId={CustomerId}",
                request.ProductId, customerId);
            throw;
        }
    }

    public async Task<ServiceResult<OrderPreviewResponse>> PreviewOrderAsync(
        string customerId, OrderPreviewRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug("PreviewOrder: CustomerId={CustomerId} ProductId={ProductId} Side={Side} QuoteSize={QuoteSize}",
            customerId, request.ProductId, request.Side, request.QuoteSize);
        try
        {
            var cbRequest = new CbOrderPreviewRequest
            {
                ProductId = request.ProductId,
                Side = request.Side.ToString().ToUpperInvariant(),
                OrderConfiguration = BuildOrderConfiguration(request.Side, request.QuoteSize, request.LimitPrice)
            };

            var preview = await _coinbase.PreviewOrderAsync(cbRequest, ct);

            if (!string.IsNullOrEmpty(preview.PreviewFailureReason))
            {
                _logger.LogWarning("Order preview rejected: {Reason} CustomerId={CustomerId}", preview.PreviewFailureReason, customerId);
                return ServiceResult<OrderPreviewResponse>.Failure(
                    "PREVIEW_FAILED", preview.PreviewFailureReason);
            }

            var slippage = decimal.TryParse(preview.Slippage, out var s) ? s : 0m;
            var bestBid = decimal.Parse(preview.BestBid);
            var bestAsk = decimal.Parse(preview.BestAsk);

            _logger.LogDebug("Order preview succeeded: Commission={Commission} Slippage={Slippage}", preview.CommissionTotal, slippage);

            return ServiceResult<OrderPreviewResponse>.Success(new OrderPreviewResponse(
                BaseSize: decimal.Parse(preview.BaseSize),
                QuoteSize: decimal.Parse(preview.QuoteSize),
                CommissionTotal: decimal.Parse(preview.CommissionTotal),
                BestBidAskPrice: request.Side == OrderSide.Buy ? bestAsk : bestBid,
                AverageFilledPrice: decimal.Parse(preview.AverageFilledPrice),
                OrderType: preview.OrderType,
                SlippageWarning: slippage > 0.01m,
                SlippagePercentage: slippage > 0 ? slippage : null));
        }
        catch (CoinbaseApiException ex)
        {
            _logger.LogWarning(ex, "Coinbase API error previewing order for CustomerId={CustomerId}", customerId);
            return ServiceResult<OrderPreviewResponse>.Failure(ex.ErrorCode, ex.Message, ex.HttpStatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error previewing order for CustomerId={CustomerId}", customerId);
            throw;
        }
    }

    public async Task<ServiceResult<PlaceOrderResponse>> PlaceOrderAsync(
        string customerId, PlaceOrderRequest request, CancellationToken ct = default)
    {
        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();

        // 1. Idempotency check
        var existing = await _orderRepo.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing != null)
        {
            _logger.LogInformation("Idempotent replay for key {Key}, CustomerId {CustomerId}", idempotencyKey, customerId);
            return ServiceResult<PlaceOrderResponse>.Success(MapToPlaceOrderResponse(existing));
        }

        // 2. Verify customer wallet exists
        var wallet = await _walletRepo.GetByCustomerIdAsync(customerId, ct);
        if (wallet == null)
            return ServiceResult<PlaceOrderResponse>.NotFound(
                $"No wallet found for customer. Please contact support.", "WALLET_NOT_FOUND");

        // 3. Create local record first (crash-safe)
        var clientOrderId = $"bank-{Guid.NewGuid():N}";
        var order = StablecoinOrder.Create(
            customerId, request.ProductId, request.Side,
            request.QuoteSize, clientOrderId, idempotencyKey);
        await _orderRepo.AddAsync(order, ct);

        // 4. Call Coinbase
        try
        {
            var cbRequest = new CbCreateOrderRequest
            {
                ClientOrderId = clientOrderId,
                ProductId = request.ProductId,
                Side = request.Side.ToString().ToUpperInvariant(),
                OrderConfiguration = BuildOrderConfiguration(request.Side, request.QuoteSize, request.LimitPrice)
            };

            var result = await _coinbase.CreateOrderAsync(cbRequest, ct);

            if (!result.Success || result.SuccessResponse == null)
            {
                var errorCode = result.ErrorResponse?.Error ?? "ORDER_FAILED";
                var errorMsg = result.ErrorResponse?.Message ?? result.FailureReason ?? "Order placement failed.";
                order.UpdateStatus(OrderStatus.Failed, failureReason: errorMsg,
                    rawResponse: JsonSerializer.Serialize(result));
                await _orderRepo.UpdateAsync(order, ct);
                return ServiceResult<PlaceOrderResponse>.Failure(errorCode, errorMsg);
            }

            order.UpdateStatus(OrderStatus.Open,
                coinbaseOrderId: result.SuccessResponse.OrderId,
                rawResponse: JsonSerializer.Serialize(result));
        }
        catch (CoinbaseApiException ex)
        {
            _logger.LogError(ex, "Coinbase API error placing order for CustomerId {CustomerId}", customerId);
            order.UpdateStatus(OrderStatus.Failed, failureReason: ex.Message);
            await _orderRepo.UpdateAsync(order, ct);
            return ServiceResult<PlaceOrderResponse>.Failure(ex.ErrorCode, ex.Message, ex.HttpStatusCode);
        }

        await _orderRepo.UpdateAsync(order, ct);
        return ServiceResult<PlaceOrderResponse>.Success(MapToPlaceOrderResponse(order));
    }

    public async Task<ServiceResult<OrderStatusResponse>> GetOrderStatusAsync(
        string customerId, Guid orderId, CancellationToken ct = default)
    {
        var order = await _orderRepo.GetByIdAsync(orderId, ct);
        if (order == null || order.CustomerId != customerId)
            return ServiceResult<OrderStatusResponse>.NotFound($"Order '{orderId}' not found.", "ORDER_NOT_FOUND");

        // Refresh from Coinbase if not terminal
        if (!order.IsTerminal && order.CoinbaseOrderId != null)
        {
            try
            {
                var cbOrder = await _coinbase.GetOrderAsync(order.CoinbaseOrderId, ct);
                if (cbOrder.Order != null)
                {
                    var newStatus = MapCoinbaseStatus(cbOrder.Order.Status);
                    order.UpdateStatus(
                        newStatus,
                        filledAmount: decimal.TryParse(cbOrder.Order.FilledSize, out var fs) ? fs : null,
                        filledValue: decimal.TryParse(cbOrder.Order.FilledValue, out var fv) ? fv : null,
                        totalFees: decimal.TryParse(cbOrder.Order.TotalFees, out var tf) ? tf : null,
                        averageFilledPrice: decimal.TryParse(cbOrder.Order.AverageFilledPrice, out var ap) ? ap : null);
                    await _orderRepo.UpdateAsync(order, ct);
                }
            }
            catch (CoinbaseApiException ex)
            {
                _logger.LogWarning(ex, "Failed to refresh order {OrderId} from Coinbase", orderId);
            }
        }

        return ServiceResult<OrderStatusResponse>.Success(MapToOrderStatusResponse(order));
    }

    public async Task<ServiceResult<PagedResult<OrderStatusResponse>>> ListOrdersAsync(
        string customerId, OrderListRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug("ListOrders: CustomerId={CustomerId} Side={Side} Status={Status} PageSize={PageSize}",
            customerId, request.Side, request.Status, request.PageSize);

        var (items, totalCount, nextCursor) = await _orderRepo.ListByCustomerAsync(
            customerId, request.Side, request.Status,
            request.FromDate, request.ToDate,
            request.PageSize, request.Cursor, ct);

        _logger.LogDebug("ListOrders returned {Count} of {Total} for CustomerId={CustomerId}", items.Count(), totalCount, customerId);

        return ServiceResult<PagedResult<OrderStatusResponse>>.Success(new PagedResult<OrderStatusResponse>
        {
            Items = items.Select(MapToOrderStatusResponse).ToList(),
            TotalCount = totalCount,
            PageSize = request.PageSize,
            NextCursor = nextCursor
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static CbOrderConfiguration BuildOrderConfiguration(
        OrderSide side, decimal quoteSize, decimal? limitPrice)
    {
        if (limitPrice.HasValue)
        {
            return new CbOrderConfiguration
            {
                LimitOrder = new CbLimitOrder
                {
                    BaseSize = quoteSize.ToString("F8"),
                    LimitPrice = limitPrice.Value.ToString("F8")
                }
            };
        }

        return new CbOrderConfiguration
        {
            MarketOrder = side == OrderSide.Buy
                ? new CbMarketOrder { QuoteSize = quoteSize.ToString("F2") }
                : new CbMarketOrder { BaseSize = quoteSize.ToString("F8") }
        };
    }

    private static OrderStatus MapCoinbaseStatus(string cbStatus) => cbStatus switch
    {
        "OPEN" => OrderStatus.Open,
        "PENDING" => OrderStatus.Pending,
        "FILLED" => OrderStatus.Filled,
        "CANCELLED" or "CANCELED" => OrderStatus.Cancelled,
        "EXPIRED" => OrderStatus.Expired,
        _ => OrderStatus.Failed
    };

    private static PlaceOrderResponse MapToPlaceOrderResponse(StablecoinOrder order) =>
        new(order.Id, order.CoinbaseOrderId, order.ProductId, order.Side, order.Status,
            order.RequestedAmount, order.FilledAmount, order.FilledValue,
            order.TotalFees, order.CreatedAt);

    private static OrderStatusResponse MapToOrderStatusResponse(StablecoinOrder order) =>
        new(order.Id, order.CoinbaseOrderId, order.ProductId, order.Side, order.Status,
            order.RequestedAmount, order.FilledAmount, order.FilledValue,
            order.TotalFees, order.AverageFilledPrice, order.FailureReason,
            order.CreatedAt, order.CompletedAt);
}
