using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using OpenBanking.StableCoin.Infrastructure.Coinbase.Webhook;
using OpenBanking.StableCoin.Infrastructure.Configuration;

namespace OpenBanking.StableCoin.Infrastructure.Tests.Webhook;

public class CoinbaseWebhookValidatorTests
{
    private const string Secret = "test-webhook-secret-abc123";

    private readonly CoinbaseWebhookValidator _sut;

    public CoinbaseWebhookValidatorTests()
    {
        var options = Options.Create(new CoinbaseOptions { WebhookSecret = Secret });
        _sut = new CoinbaseWebhookValidator(options);
    }

    [Fact]
    public void ValidateSignature_WithValidHmac_ReturnsTrue()
    {
        const string payload = """{"type":"order.filled","order_id":"abc123"}""";
        const string timestamp = "1700000000";

        var signature = ComputeHmac(Secret, timestamp + payload);

        _sut.ValidateSignature(payload, signature, timestamp).Should().BeTrue();
    }

    [Fact]
    public void ValidateSignature_WithTamperedPayload_ReturnsFalse()
    {
        const string originalPayload = """{"type":"order.filled","order_id":"abc123"}""";
        const string tamperedPayload = """{"type":"order.filled","order_id":"evil"}""";
        const string timestamp = "1700000000";

        var signature = ComputeHmac(Secret, timestamp + originalPayload);

        _sut.ValidateSignature(tamperedPayload, signature, timestamp).Should().BeFalse();
    }

    [Fact]
    public void ValidateSignature_WithWrongSecret_ReturnsFalse()
    {
        const string payload = """{"type":"order.filled"}""";
        const string timestamp = "1700000000";

        var signature = ComputeHmac("wrong-secret", timestamp + payload);

        _sut.ValidateSignature(payload, signature, timestamp).Should().BeFalse();
    }

    [Fact]
    public void IsTimestampFresh_WithRecentTimestamp_ReturnsTrue()
    {
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-60).ToUnixTimeSeconds().ToString();
        _sut.IsTimestampFresh(timestamp).Should().BeTrue();
    }

    [Fact]
    public void IsTimestampFresh_WithStaleTimestamp_ReturnsFalse()
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        _sut.IsTimestampFresh(timestamp).Should().BeFalse();
    }

    private static string ComputeHmac(string secret, string message)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
