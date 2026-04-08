using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using OpenBanking.StableCoin.Application.Exceptions;
using OpenBanking.StableCoin.Application.Interfaces.ExternalClients;
using OpenBanking.StableCoin.Application.Models.Coinbase;

namespace OpenBanking.StableCoin.Infrastructure.Coinbase.Clients;

public sealed class CoinbaseAdvancedTradeClient : ICoinbaseAdvancedTradeClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CoinbaseAdvancedTradeClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<CbProductResponse> GetProductAsync(string productId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"products/{productId}", ct);
        return await ReadResponseAsync<CbProductResponse>(response, ct);
    }

    public async Task<CbOrderPreviewResponse> PreviewOrderAsync(
        CbOrderPreviewRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("orders/preview", request, JsonOpts, ct);
        return await ReadResponseAsync<CbOrderPreviewResponse>(response, ct);
    }

    public async Task<CbCreateOrderResponse> CreateOrderAsync(
        CbCreateOrderRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("orders", request, JsonOpts, ct);
        return await ReadResponseAsync<CbCreateOrderResponse>(response, ct);
    }

    public async Task<CbOrderResponse> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"orders/historical/{orderId}", ct);
        return await ReadResponseAsync<CbOrderResponse>(response, ct);
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

        var response = await _http.GetAsync($"orders/historical/batch?{query}", ct);
        return await ReadResponseAsync<CbListOrdersResponse>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return JsonSerializer.Deserialize<T>(body, JsonOpts)
                   ?? throw new CoinbaseApiException("EMPTY_RESPONSE", "Coinbase returned an empty response.");
        }

        // Parse error
        string errorCode = "COINBASE_ERROR";
        string message = $"Coinbase API error {(int)response.StatusCode}";
        try
        {
            var error = JsonSerializer.Deserialize<JsonElement>(body, JsonOpts);
            if (error.TryGetProperty("error", out var e)) errorCode = e.GetString() ?? errorCode;
            if (error.TryGetProperty("message", out var m)) message = m.GetString() ?? message;
        }
        catch { /* use defaults */ }

        throw new CoinbaseApiException(errorCode, message, (int)response.StatusCode);
    }
}
