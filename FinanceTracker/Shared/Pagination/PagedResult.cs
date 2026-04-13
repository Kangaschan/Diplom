namespace Shared.Pagination;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Total, int Page, int PageSize);
