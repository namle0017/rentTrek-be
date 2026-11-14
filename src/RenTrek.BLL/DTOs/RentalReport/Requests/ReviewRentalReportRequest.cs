using RenTrek.BLL.DTOs.RentalOrder.Responses;

namespace RenTrek.BLL.DTOs.RentalReport.Requests;

/// <summary>Admin duyệt / từ chối báo cáo.</summary>
public class ReviewRentalReportRequest
{
    public string? ReviewNote { get; set; }
    public int ExpectedRowVersion { get; set; }
}
