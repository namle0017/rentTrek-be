using RenTrek.BLL.DTOs.RentalOrder.Responses;

namespace RenTrek.BLL.DTOs.RentalReport.Responses;

/// <summary>Báo cáo đơn thuê gửi admin.</summary>
public class RentalReportResponse
{
    public Guid RentalReportId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    /// <summary>NoShow | LateCancellation | ReturnInspection | SuspectedLoss | ExtraPayment.</summary>
    public string ReportType { get; set; } = string.Empty;
    /// <summary>Submitted | Approved | Rejected | Resolved.</summary>
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public string? ReviewNote { get; set; }
    /// <summary>null = hệ thống tự tạo.</summary>
    public string? CreatedByName { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int RowVersion { get; set; }
    /// <summary>Chỉ có ở chi tiết: ảnh bằng chứng của đơn.</summary>
    public List<RentalEvidencePhotoResponse> EvidencePhotos { get; set; } = [];
}
