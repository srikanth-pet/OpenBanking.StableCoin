using System.Text.Json.Serialization;

namespace OpenBanking.StableCoin.Application.Models.Coinbase;

// ─── Advanced Trade: Product ───────────────────────────────────────────────
public sealed class CbProductResponse
{
    [JsonPropertyName("product_id")] public string ProductId { get; init; } = string.Empty;
    [JsonPropertyName("price")] public string Price { get; init; } = "0";
    [JsonPropertyName("best_bid")] public string BestBid { get; init; } = "0";
    [JsonPropertyName("best_ask")] public string BestAsk { get; init; } = "0";
    [JsonPropertyName("volume_24h")] public string Volume24h { get; init; } = "0";
    [JsonPropertyName("quote_currency_id")] public string QuoteCurrencyId { get; init; } = string.Empty;
    [JsonPropertyName("base_currency_id")] public string BaseCurrencyId { get; init; } = string.Empty;
}

// ─── Advanced Trade: Order Preview ────────────────────────────────────────
public sealed class CbOrderPreviewRequest
{
    [JsonPropertyName("product_id")] public string ProductId { get; init; } = string.Empty;
    [JsonPropertyName("side")] public string Side { get; init; } = string.Empty;
    [JsonPropertyName("order_configuration")] public CbOrderConfiguration OrderConfiguration { get; init; } = new();
}

public sealed class CbOrderPreviewResponse
{
    [JsonPropertyName("order_total")] public string OrderTotal { get; init; } = "0";
    [JsonPropertyName("commission_total")] public string CommissionTotal { get; init; } = "0";
    [JsonPropertyName("errs")] public List<string> Errors { get; init; } = [];
    [JsonPropertyName("warning")] public List<string> Warnings { get; init; } = [];
    [JsonPropertyName("quote_size")] public string QuoteSize { get; init; } = "0";
    [JsonPropertyName("base_size")] public string BaseSize { get; init; } = "0";
    [JsonPropertyName("best_bid")] public string BestBid { get; init; } = "0";
    [JsonPropertyName("best_ask")] public string BestAsk { get; init; } = "0";
    [JsonPropertyName("average_filled_price")] public string AverageFilledPrice { get; init; } = "0";
    [JsonPropertyName("slippage")] public string Slippage { get; init; } = "0";
    [JsonPropertyName("preview_failure_reason")] public string? PreviewFailureReason { get; init; }
    [JsonPropertyName("order_type")] public string OrderType { get; init; } = "MARKET";
}

// ─── Advanced Trade: Create Order ─────────────────────────────────────────
public sealed class CbCreateOrderRequest
{
    [JsonPropertyName("client_order_id")] public string ClientOrderId { get; init; } = string.Empty;
    [JsonPropertyName("product_id")] public string ProductId { get; init; } = string.Empty;
    [JsonPropertyName("side")] public string Side { get; init; } = string.Empty;
    [JsonPropertyName("order_configuration")] public CbOrderConfiguration OrderConfiguration { get; init; } = new();
}

public sealed class CbOrderConfiguration
{
    [JsonPropertyName("market_market_ioc")] public CbMarketOrder? MarketOrder { get; init; }
    [JsonPropertyName("limit_limit_gtc")] public CbLimitOrder? LimitOrder { get; init; }
}

public sealed class CbMarketOrder
{
    [JsonPropertyName("quote_size")] public string? QuoteSize { get; init; }
    [JsonPropertyName("base_size")] public string? BaseSize { get; init; }
}

public sealed class CbLimitOrder
{
    [JsonPropertyName("base_size")] public string BaseSize { get; init; } = string.Empty;
    [JsonPropertyName("limit_price")] public string LimitPrice { get; init; } = string.Empty;
    [JsonPropertyName("post_only")] public bool PostOnly { get; init; } = false;
}

public sealed class CbCreateOrderResponse
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("failure_reason")] public string? FailureReason { get; init; }
    [JsonPropertyName("order_id")] public string? OrderId { get; init; }
    [JsonPropertyName("success_response")] public CbOrderSuccessResponse? SuccessResponse { get; init; }
    [JsonPropertyName("error_response")] public CbOrderErrorResponse? ErrorResponse { get; init; }
}

public sealed class CbOrderSuccessResponse
{
    [JsonPropertyName("order_id")] public string OrderId { get; init; } = string.Empty;
    [JsonPropertyName("product_id")] public string ProductId { get; init; } = string.Empty;
    [JsonPropertyName("side")] public string Side { get; init; } = string.Empty;
    [JsonPropertyName("client_order_id")] public string ClientOrderId { get; init; } = string.Empty;
}

public sealed class CbOrderErrorResponse
{
    [JsonPropertyName("error")] public string Error { get; init; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("error_details")] public string? ErrorDetails { get; init; }
    [JsonPropertyName("preview_failure_reason")] public string? PreviewFailureReason { get; init; }
}

// ─── Advanced Trade: Order Status ─────────────────────────────────────────
public sealed class CbOrderResponse
{
    [JsonPropertyName("order")] public CbOrder? Order { get; init; }
}

public sealed class CbOrder
{
    [JsonPropertyName("order_id")] public string OrderId { get; init; } = string.Empty;
    [JsonPropertyName("product_id")] public string ProductId { get; init; } = string.Empty;
    [JsonPropertyName("user_id")] public string UserId { get; init; } = string.Empty;
    [JsonPropertyName("side")] public string Side { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("client_order_id")] public string ClientOrderId { get; init; } = string.Empty;
    [JsonPropertyName("created_time")] public DateTimeOffset CreatedTime { get; init; }
    [JsonPropertyName("completion_percentage")] public string CompletionPercentage { get; init; } = "0";
    [JsonPropertyName("filled_size")] public string FilledSize { get; init; } = "0";
    [JsonPropertyName("average_filled_price")] public string AverageFilledPrice { get; init; } = "0";
    [JsonPropertyName("fee")] public string Fee { get; init; } = "0";
    [JsonPropertyName("total_fees")] public string TotalFees { get; init; } = "0";
    [JsonPropertyName("filled_value")] public string FilledValue { get; init; } = "0";
    [JsonPropertyName("pending_cancel_count")] public int PendingCancelCount { get; init; }
    [JsonPropertyName("size_in_quote")] public bool SizeInQuote { get; init; }
    [JsonPropertyName("order_configuration")] public CbOrderConfiguration? OrderConfiguration { get; init; }
}

// ─── Advanced Trade: List Orders ──────────────────────────────────────────
public sealed class CbListOrdersRequest
{
    public string? ProductId { get; init; }
    public string? Side { get; init; }
    public List<string>? OrderStatuses { get; init; }
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public int Limit { get; init; } = 20;
    public string? Cursor { get; init; }
}

public sealed class CbListOrdersResponse
{
    [JsonPropertyName("orders")] public List<CbOrder> Orders { get; init; } = [];
    [JsonPropertyName("sequence")] public string Sequence { get; init; } = string.Empty;
    [JsonPropertyName("has_next")] public bool HasNext { get; init; }
    [JsonPropertyName("cursor")] public string? Cursor { get; init; }
}

// ─── CDP Wallet ────────────────────────────────────────────────────────────
public sealed class CbWalletResponse
{
    [JsonPropertyName("wallet")] public CbWallet? Wallet { get; init; }
}

public sealed class CbWallet
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
}

public sealed class CbAddressResponse
{
    [JsonPropertyName("address")] public string Address { get; init; } = string.Empty;
    [JsonPropertyName("address_info")] public CbAddressInfo? AddressInfo { get; init; }
    [JsonPropertyName("network")] public string Network { get; init; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; init; }
}

public sealed class CbAddressInfo
{
    [JsonPropertyName("address")] public string Address { get; init; } = string.Empty;
}

public sealed class CbTransferRequest
{
    [JsonPropertyName("to")] public string To { get; init; } = string.Empty;
    [JsonPropertyName("amount")] public string Amount { get; init; } = string.Empty;
    [JsonPropertyName("currency")] public string Currency { get; init; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; init; } = "send";
    [JsonPropertyName("idem")] public string? IdempotencyKey { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
}

public sealed class CbTransferResponse
{
    [JsonPropertyName("data")] public CbTransferData? Data { get; init; }
}

public sealed class CbTransferData
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("amount")] public CbAmount? Amount { get; init; }
    [JsonPropertyName("network")] public CbTransferNetwork? Network { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; init; }
}

public sealed class CbAmount
{
    [JsonPropertyName("amount")] public string Amount { get; init; } = "0";
    [JsonPropertyName("currency")] public string Currency { get; init; } = string.Empty;
}

public sealed class CbTransferNetwork
{
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("hash")] public string? Hash { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
}

public sealed class CbBalanceResponse
{
    [JsonPropertyName("data")] public CbAmount? Data { get; init; }
}

public sealed class CbTransferListResponse
{
    [JsonPropertyName("data")] public List<CbTransferData> Data { get; init; } = [];
    [JsonPropertyName("pagination")] public CbPagination? Pagination { get; init; }
}

public sealed class CbPagination
{
    [JsonPropertyName("next_uri")] public string? NextUri { get; init; }
    [JsonPropertyName("next_starting_after")] public string? NextStartingAfter { get; init; }
}

// ─── Webhook ───────────────────────────────────────────────────────────────
public sealed class CbWebhookEvent
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("data")] public Dictionary<string, object?> Data { get; init; } = [];
}
