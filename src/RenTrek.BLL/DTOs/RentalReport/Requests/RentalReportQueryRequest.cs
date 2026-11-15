using RenTrek.BLL.DTOs.RentalOrder.Responses;

namespace RenTrek.BLL.DTOs.RentalReport.Requests;

public class RentalReportQueryRequest
{
    public string? Status { get; set; }
    public string? ReportType { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
