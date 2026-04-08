namespace OpenBanking.StableCoin.Infrastructure.Configuration;

public sealed class CoinbaseOptions
{
    public const string SectionKey = "Coinbase";

    public string ApiKeyName { get; init; } = string.Empty;
    public string PrivateKeyPem { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;
    public string DefaultProductId { get; init; } = "USDC-USD";
    public string[] SupportedNetworks { get; init; } = ["base-mainnet", "ethereum-mainnet", "polygon-mainnet"];
    public string AdvancedTradeBaseUrl { get; init; } = "https://api.coinbase.com/api/v3/brokerage/";
    public string CdpWalletBaseUrl { get; init; } = "https://api.cdp.coinbase.com/platform/v1/";
}
