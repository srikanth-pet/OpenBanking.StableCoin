namespace OpenBanking.StableCoin.Application.Common;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageSize { get; init; }
    public string? NextCursor { get; init; }
    public bool HasNextPage => NextCursor != null;
}

public sealed class PaginationRequest
{
    public int PageSize { get; init; } = 20;
    public string? Cursor { get; init; }
}
