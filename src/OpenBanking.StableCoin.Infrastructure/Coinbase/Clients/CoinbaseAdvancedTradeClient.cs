using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using OpenBanking.StableCoin.Application.Exceptions;
using OpenBanking.StableCoin.Application.Interfaces.ExternalClients;
using OpenBanking.StableCoin.Application.Models.Coinbase;

namespace OpenBanking.StableCoin.Infrastructure.Coinbase.Clients;

public sealed class CoinbaseAdvancedTradeClient : ICoinbaseAdvancedTradeClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CoinbaseAdvancedTradeClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CoinbaseAdvancedTradeClient(HttpClient http, ILogger<CoinbaseAdvancedTradeClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<CbProductResponse> GetProductAsync(string productId, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching Coinbase product {ProductId}", productId);
        var response = await _http.GetAsync($"products/{productId}", ct);
        return await ReadResponseAsync<CbProductResponse>(response, $"GET products/{productId}", ct);
    }

    public async Task<CbOrderPreviewResponse> PreviewOrderAsync(
        CbOrderPreviewRequest request, CancellationToken ct = default)
    {
        _logger.LogDebug("Previewing order for product {ProductId} side {Side}", request.ProductId, request.Side);
        var response = await _http.PostAsJsonAsync("orders/preview", request, JsonOpts, ct);
        return await ReadResponseAsync<CbOrderPreviewResponse>(response, "POST orders/preview", ct);
    }

    public async Task<CbCreateOrderResponse> CreateOrderAsync(
        CbCreateOrderRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Placing order: ClientOrderId={ClientOrderId} ProductId={ProductId} Side={Side}",
            request.ClientOrderId, request.ProductId, request.Side);
        var response = await _http.PostAsJsonAsync("orders", request, JsonOpts, ct);
        return await ReadResponseAsync<CbCreateOrderResponse>(response, "POST orders", ct);
    }

    public async Task<CbOrderResponse> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching Coinbase order {OrderId}", orderId);
        var response = await _http.GetAsync($"orders/historical/{orderId}", ct);
        return await ReadResponseAsync<CbOrderResponse>(response, $"GET orders/historical/{orderId}", ct);
    }

    public async Task<CbListOrdersResponse> ListOrdersAsync(
        CbListOrdersRequest request, CancellationToken ct = default)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        if (request.ProductId != null) query["product_id"] = request.ProductId;
        if (request.Side != null) query["order_side"] = request.Side;
        if (request.StartDate.HasValue) query["start_date"] = request.StartDate.Value.ToString("o");
        if (request.EndDate.HasValue) query["end_date"] = request.EndDate.Value.ToString("o");
        if (request.OrderStatuses?.Count > 0)
            foreach (var s in request.OrderStatuses) query.Add("order_status", s);
        query["limit"] = request.Limit.ToString();
        if (request.Cursor != null) query["cursor"] = request.Cursor;

        _logger.LogDebug("Listing Coinbase orders: ProductId={ProductId} Limit={Limit}", request.ProductId, request.Limit);
        var response = await _http.GetAsync($"orders/historical/batch?{query}", ct);
        return await ReadResponseAsync<CbListOrdersResponse>(response, "GET orders/historical/batch", ct);
    }

    private async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response, string operation, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var statusCode = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Coinbase {Operation} succeeded with {StatusCode}", operation, statusCode);
            return JsonSerializer.Deserialize<T>(body, JsonOpts)
                   ?? throw new CoinbaseApiException("EMPTY_RESPONSE", "Coinbase returned an empty response.");
        }

        // Parse error
        string errorCode = "COINBASE_ERROR";
        string message = $"Coinbase API error {statusCode}";
        try
        {
            var error = JsonSerializer.Deserialize<JsonElement>(body, JsonOpts);
            if (error.TryGetProperty("error", out var e)) errorCode = e.GetString() ?? errorCode;
            if (error.TryGetProperty("message", out var m)) message = m.GetString() ?? message;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse Coinbase error response body for {Operation}. Raw body: {Body}",
                operation, body.Length > 500 ? body[..500] : body);
        }

        _logger.LogWarning("Coinbase {Operation} failed: {StatusCode} {ErrorCode} — {Message}",
            operation, statusCode, errorCode, message);

        throw new CoinbaseApiException(errorCode, message, statusCode);
    }
}
