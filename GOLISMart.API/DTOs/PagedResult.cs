namespace GOLISMart.API.DTOs;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

public record ProductQueryParams
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? Category { get; init; }
    public string? SortBy { get; init; } = "name";
    public string? SortOrder { get; init; } = "asc";
    public bool? InStock { get; init; }
}
