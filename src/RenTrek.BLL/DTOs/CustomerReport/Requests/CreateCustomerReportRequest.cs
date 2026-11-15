namespace RenTrek.BLL.DTOs.CustomerReport.Requests;

/// <summary>Khách gửi báo cáo về đơn thuê hoặc booking chuyến đi.</summary>
public sealed class CreateCustomerReportRequest
{
    /// <summary>Rental | Trip.</summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>Mã đơn (orders.order_id) của đơn thuê hoặc booking chuyến.</summary>
    public Guid OrderId { get; set; }

    /// <summary>Rental: Damaged | WrongVariant | MissingAccessory | NotAsDescribed | Other. Trip: Guide | Schedule | Safety | Service | Equipment | Payment | Other.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Thiết bị / hoạt động liên quan (tuỳ chọn, tối đa 255 ký tự).</summary>
    public string? SubjectLabel { get; set; }

    /// <summary>Mô tả, 10–2000 ký tự.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Exchange | Refund | Repair | Feedback | Other (tuỳ chọn).</summary>
    public string? PreferredAction { get; set; }

    public string? ContactPhone { get; set; }

    /// <summary>Tối đa 5 link ảnh https đã tải lên qua POST /customer-reports/images.</summary>
    public List<string> PhotoUrls { get; set; } = [];
}
