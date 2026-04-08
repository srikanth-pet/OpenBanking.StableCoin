namespace OpenBanking.StableCoin.Application.Exceptions;

public class CoinbaseApiException : Exception
{
    public string ErrorCode { get; }
    public int HttpStatusCode { get; }

    public CoinbaseApiException(string errorCode, string message, int httpStatusCode = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        HttpStatusCode = httpStatusCode;
    }
}
