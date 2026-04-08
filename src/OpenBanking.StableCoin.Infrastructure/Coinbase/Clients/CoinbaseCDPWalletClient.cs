using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenBanking.StableCoin.Application.Exceptions;
using OpenBanking.StableCoin.Application.Interfaces.ExternalClients;
using OpenBanking.StableCoin.Application.Models.Coinbase;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Infrastructure.Coinbase.Clients;

public sealed class CoinbaseCDPWalletClient : ICoinbaseCDPWalletClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CoinbaseCDPWalletClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Dictionary<SupportedNetwork, string> NetworkIds = new()
    {
        [SupportedNetwork.Base] = "base-mainnet",
        [SupportedNetwork.Ethereum] = "ethereum-mainnet",
        [SupportedNetwork.Polygon] = "polygon-mainnet"
    };

    public CoinbaseCDPWalletClient(HttpClient http, ILogger<CoinbaseCDPWalletClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<CbWalletResponse> CreateWalletAsync(string customerId, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating CDP wallet for customer {CustomerId}", customerId);
        var payload = new { name = $"customer-{customerId}" };
        var response = await _http.PostAsJsonAsync("wallets", payload, JsonOpts, ct);
        return await ReadResponseAsync<CbWalletResponse>(response, "POST wallets", ct);
    }

    public async Task<CbAddressResponse> GetOrCreateDepositAddressAsync(
        string walletId, SupportedNetwork network, CancellationToken ct = default)
    {
        var networkId = NetworkIds[network];
        _logger.LogDebug("Creating deposit address for wallet {WalletId} on network {Network}", walletId, networkId);
        var payload = new { network = networkId };
        var response = await _http.PostAsJsonAsync(
            $"wallets/{walletId}/addresses", payload, JsonOpts, ct);
        return await ReadResponseAsync<CbAddressResponse>(response, $"POST wallets/{walletId}/addresses", ct);
    }

    public async Task<CbTransferResponse> TransferAsync(
        string walletId, CbTransferRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Initiating CDP transfer from wallet {WalletId}: {Amount} {Currency} to {To}",
            walletId, request.Amount, request.Currency, request.To);
        var response = await _http.PostAsJsonAsync(
            $"wallets/{walletId}/transactions", request, JsonOpts, ct);
        return await ReadResponseAsync<CbTransferResponse>(response, $"POST wallets/{walletId}/transactions", ct);
    }

    public async Task<CbBalanceResponse> GetBalanceAsync(
        string walletId, string assetId, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching CDP balance for wallet {WalletId} asset {AssetId}", walletId, assetId);
        var response = await _http.GetAsync(
            $"wallets/{walletId}/currencies/{assetId}/balances/available", ct);
        return await ReadResponseAsync<CbBalanceResponse>(response, $"GET wallets/{walletId}/balances", ct);
    }

    public async Task<CbTransferListResponse> ListTransfersAsync(
        string walletId, int pageSize = 20, string? cursor = null, CancellationToken ct = default)
    {
        var url = $"wallets/{walletId}/transactions?limit={pageSize}";
        if (cursor != null) url += $"&starting_after={cursor}";
        _logger.LogDebug("Listing CDP transfers for wallet {WalletId} pageSize={PageSize}", walletId, pageSize);
        var response = await _http.GetAsync(url, ct);
        return await ReadResponseAsync<CbTransferListResponse>(response, $"GET wallets/{walletId}/transactions", ct);
    }

    private async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var statusCode = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("CDP {Operation} succeeded with {StatusCode}", operation, statusCode);
            return JsonSerializer.Deserialize<T>(body, JsonOpts)
                   ?? throw new CoinbaseApiException("EMPTY_RESPONSE", "CDP returned an empty response.");
        }

        string errorCode = "CDP_ERROR";
        string message = $"Coinbase CDP error {statusCode}";
        try
        {
            var error = JsonSerializer.Deserialize<JsonElement>(body, JsonOpts);
            if (error.TryGetProperty("code", out var c)) errorCode = c.GetString() ?? errorCode;
            if (error.TryGetProperty("message", out var m)) message = m.GetString() ?? message;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse CDP error response body for {Operation}. Raw body: {Body}",
                operation, body.Length > 500 ? body[..500] : body);
        }

        _logger.LogWarning("CDP {Operation} failed: {StatusCode} {ErrorCode} — {Message}",
            operation, statusCode, errorCode, message);

        throw new CoinbaseApiException(errorCode, message, statusCode);
    }
}
