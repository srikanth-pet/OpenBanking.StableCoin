using System.Net.Http.Headers;

namespace OpenBanking.StableCoin.Infrastructure.Coinbase.Auth;

public sealed class CoinbaseAuthHandler : DelegatingHandler
{
    private readonly ICoinbaseJwtTokenGenerator _tokenGenerator;

    public CoinbaseAuthHandler(ICoinbaseJwtTokenGenerator tokenGenerator)
    {
        _tokenGenerator = tokenGenerator;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var token = _tokenGenerator.GenerateToken(
            request.Method.Method,
            uri.Host,
            uri.PathAndQuery);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
