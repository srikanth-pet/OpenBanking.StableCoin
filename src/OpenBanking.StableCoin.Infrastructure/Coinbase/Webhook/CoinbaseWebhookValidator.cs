using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OpenBanking.StableCoin.Infrastructure.Configuration;

namespace OpenBanking.StableCoin.Infrastructure.Coinbase.Webhook;

public interface ICoinbaseWebhookValidator
{
    bool ValidateSignature(string payload, string signature, string timestamp);
    bool IsTimestampFresh(string timestamp, int maxAgeSeconds = 300);
}

public sealed class CoinbaseWebhookValidator : ICoinbaseWebhookValidator
{
    private readonly CoinbaseOptions _options;

    public CoinbaseWebhookValidator(IOptions<CoinbaseOptions> options)
    {
        _options = options.Value;
    }

    public bool ValidateSignature(string payload, string signature, string timestamp)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(timestamp))
            return false;

        var message = timestamp + payload;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        // Constant-time comparison prevents timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }

    public bool IsTimestampFresh(string timestamp, int maxAgeSeconds = 300)
    {
        if (!long.TryParse(timestamp, out var unixTime))
            return false;

        var eventTime = DateTimeOffset.FromUnixTimeSeconds(unixTime);
        var age = DateTimeOffset.UtcNow - eventTime;
        return Math.Abs(age.TotalSeconds) <= maxAgeSeconds;
    }
}
