namespace RenTrek.BLL.DTOs.Common;

/// <summary>
/// Query chung cho API danh sách. Request filter riêng của từng module kế thừa class này.
/// </summary>
public class PagingRequest
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    private int _pageNumber = 1;
    private int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }

    public string? Search { get; set; }

    public string SortBy { get; set; } = "createdAt";

    /// <summary>asc | desc</summary>
    public string SortOrder { get; set; } = "desc";

    public bool IsDescending => string.Equals(SortOrder, "desc", StringComparison.OrdinalIgnoreCase);
}
