using System.Net.Http.Json;
using System.Text.Json;
using OpenBanking.StableCoin.Application.Exceptions;
using OpenBanking.StableCoin.Application.Interfaces.ExternalClients;
using OpenBanking.StableCoin.Application.Models.Coinbase;
using OpenBanking.StableCoin.Domain.Enums;

namespace OpenBanking.StableCoin.Infrastructure.Coinbase.Clients;

public sealed class CoinbaseCDPWalletClient : ICoinbaseCDPWalletClient
{
    private readonly HttpClient _http;

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

    public CoinbaseCDPWalletClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<CbWalletResponse> CreateWalletAsync(string customerId, CancellationToken ct = default)
    {
        var payload = new { name = $"customer-{customerId}" };
        var response = await _http.PostAsJsonAsync("wallets", payload, JsonOpts, ct);
        return await ReadResponseAsync<CbWalletResponse>(response, ct);
    }

    public async Task<CbAddressResponse> GetOrCreateDepositAddressAsync(
        string walletId, SupportedNetwork network, CancellationToken ct = default)
    {
        var networkId = NetworkIds[network];
        var payload = new { network = networkId };
        var response = await _http.PostAsJsonAsync(
            $"wallets/{walletId}/addresses", payload, JsonOpts, ct);
        return await ReadResponseAsync<CbAddressResponse>(response, ct);
    }

    public async Task<CbTransferResponse> TransferAsync(
        string walletId, CbTransferRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"wallets/{walletId}/transactions", request, JsonOpts, ct);
        return await ReadResponseAsync<CbTransferResponse>(response, ct);
    }

    public async Task<CbBalanceResponse> GetBalanceAsync(
        string walletId, string assetId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(
            $"wallets/{walletId}/currencies/{assetId}/balances/available", ct);
        return await ReadResponseAsync<CbBalanceResponse>(response, ct);
    }

    public async Task<CbTransferListResponse> ListTransfersAsync(
        string walletId, int pageSize = 20, string? cursor = null, CancellationToken ct = default)
    {
        var url = $"wallets/{walletId}/transactions?limit={pageSize}";
        if (cursor != null) url += $"&starting_after={cursor}";
        var response = await _http.GetAsync(url, ct);
        return await ReadResponseAsync<CbTransferListResponse>(response, ct);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return JsonSerializer.Deserialize<T>(body, JsonOpts)
                   ?? throw new CoinbaseApiException("EMPTY_RESPONSE", "CDP returned an empty response.");
        }

        string errorCode = "CDP_ERROR";
        string message = $"Coinbase CDP error {(int)response.StatusCode}";
        try
        {
            var error = JsonSerializer.Deserialize<JsonElement>(body, JsonOpts);
            if (error.TryGetProperty("code", out var c)) errorCode = c.GetString() ?? errorCode;
            if (error.TryGetProperty("message", out var m)) message = m.GetString() ?? message;
        }
        catch { /* use defaults */ }

        throw new CoinbaseApiException(errorCode, message, (int)response.StatusCode);
    }
}
