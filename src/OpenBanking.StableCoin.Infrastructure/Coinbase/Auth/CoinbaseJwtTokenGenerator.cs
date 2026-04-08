using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenBanking.StableCoin.Infrastructure.Configuration;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;

namespace OpenBanking.StableCoin.Infrastructure.Coinbase.Auth;

public interface ICoinbaseJwtTokenGenerator
{
    string GenerateToken(string requestMethod, string requestHost, string requestPath);
}

public sealed class CoinbaseJwtTokenGenerator : ICoinbaseJwtTokenGenerator
{
    private readonly CoinbaseOptions _options;
    private readonly ILogger<CoinbaseJwtTokenGenerator> _logger;

    public CoinbaseJwtTokenGenerator(
        IOptions<CoinbaseOptions> options,
        ILogger<CoinbaseJwtTokenGenerator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string GenerateToken(string requestMethod, string requestHost, string requestPath)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var uri = $"{requestMethod.ToUpperInvariant()} {requestHost}{requestPath}";

        _logger.LogDebug("Generating Coinbase JWT for {Method} {Uri}", requestMethod.ToUpperInvariant(), uri);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "ES256",
            ["kid"] = _options.ApiKeyName,
            ["nonce"] = nonce
        };

        var payload = new Dictionary<string, object>
        {
            ["sub"] = _options.ApiKeyName,
            ["iss"] = "cdp",
            ["nbf"] = now,
            ["exp"] = now + 120,
            ["uri"] = uri
        };

        var headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{headerB64}.{payloadB64}";

        Ed25519PrivateKeyParameters privateKey;
        try
        {
            privateKey = LoadEd25519PrivateKey(_options.PrivateKeyPem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to load Ed25519 private key from PEM. Ensure Coinbase:PrivateKeyPem is set correctly. " +
                "Key name: {ApiKeyName}", _options.ApiKeyName);
            throw;
        }

        try
        {
            var signer = new Ed25519Signer();
            signer.Init(true, privateKey);
            var signingBytes = Encoding.UTF8.GetBytes(signingInput);
            signer.BlockUpdate(signingBytes, 0, signingBytes.Length);
            var signature = signer.GenerateSignature();

            _logger.LogDebug("Coinbase JWT generated successfully for key {ApiKeyName}", _options.ApiKeyName);
            return $"{signingInput}.{Base64UrlEncode(signature)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign Coinbase JWT for key {ApiKeyName}", _options.ApiKeyName);
            throw;
        }
    }

    private static Ed25519PrivateKeyParameters LoadEd25519PrivateKey(string pem)
    {
        using var reader = new StringReader(pem);
        var pemReader = new PemReader(reader);
        var obj = pemReader.ReadObject();

        return obj switch
        {
            Ed25519PrivateKeyParameters key => key,
            Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair pair =>
                (Ed25519PrivateKeyParameters)pair.Private,
            _ => throw new InvalidOperationException(
                "PEM does not contain a supported Ed25519 private key.")
        };
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
