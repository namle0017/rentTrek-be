namespace RenTrek.DAL.Entities;

/// <summary>
/// Báo cáo gửi admin: khách không đến, kiểm tra trả đồ, nghi mất, yêu cầu thu thêm.
/// </summary>
public class RentalReport
{
    public Guid RentalReportId { get; set; }
    public Guid RentalOrderId { get; set; }
    public string ReportType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int RowVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public RentalOrder RentalOrder { get; set; } = null!;
    public Account? CreatedByUser { get; set; }
    public Account? ReviewedByUser { get; set; }
}
