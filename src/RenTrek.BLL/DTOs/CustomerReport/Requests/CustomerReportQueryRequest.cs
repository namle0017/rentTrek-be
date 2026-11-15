namespace RenTrek.BLL.DTOs.CustomerReport.Requests;

/// <summary>Lọc danh sách báo cáo khách ở màn admin.</summary>
public sealed class CustomerReportQueryRequest
{
    /// <summary>Rental | Trip; bỏ trống = tất cả.</summary>
    public string? SourceType { get; set; }

    /// <summary>Open | InProgress | Resolved | Rejected; bỏ trống = tất cả.</summary>
    public string? Status { get; set; }

    /// <summary>Mã đơn, tên / email / SĐT khách hoặc tên thiết bị.</summary>
    public string? Keyword { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
