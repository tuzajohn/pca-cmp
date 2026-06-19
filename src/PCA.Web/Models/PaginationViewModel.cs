namespace PCA.Web.Models;

public class PaginationViewModel
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public int PageSize { get; init; }
    public int From => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int To => Math.Min(Page * PageSize, TotalCount);
}
