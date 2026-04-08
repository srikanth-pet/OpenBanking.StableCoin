namespace OpenBanking.StableCoin.API.Models;

public sealed class ApiErrorResponse
{
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int Status { get; init; }
    public string Detail { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string? TraceId { get; init; }
    public Dictionary<string, string[]>? ValidationErrors { get; init; }
}
