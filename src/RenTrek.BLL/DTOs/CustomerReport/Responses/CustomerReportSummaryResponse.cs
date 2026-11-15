namespace RenTrek.BLL.DTOs.CustomerReport.Responses;

/// <summary>Số báo cáo theo trạng thái cho thẻ tổng quan / badge menu admin.</summary>
public sealed class CustomerReportSummaryResponse
{
    public int Open { get; set; }
    public int InProgress { get; set; }
    public int Resolved { get; set; }
    public int Rejected { get; set; }
}
