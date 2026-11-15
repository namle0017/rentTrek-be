namespace RenTrek.DAL.Entities;

/// <summary>
/// Báo cáo khách gửi về một đơn thuê (sự cố thiết bị) hoặc một booking chuyến đi; admin / staff xử lý.
/// </summary>
public sealed class CustomerReport
{
    public Guid CustomerReportId { get; set; }
    public Guid ReporterUserId { get; set; }
    public Guid OrderId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SubjectLabel { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? PreferredAction { get; set; }
    public string? ContactPhone { get; set; }
    public List<string> PhotoUrls { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public Guid? HandledByUserId { get; set; }
    public DateTimeOffset? HandledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Account ReporterUser { get; set; } = null!;
    public Order Order { get; set; } = null!;
    public Account? HandledByUser { get; set; }
}
