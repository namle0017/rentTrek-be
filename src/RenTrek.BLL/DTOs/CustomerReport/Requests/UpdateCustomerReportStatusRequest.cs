namespace RenTrek.BLL.DTOs.CustomerReport.Requests;

/// <summary>Admin / Staff cập nhật trạng thái xử lý báo cáo.</summary>
public sealed class UpdateCustomerReportStatusRequest
{
    /// <summary>InProgress | Resolved | Rejected.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Phản hồi gửi khách; bắt buộc khi Resolved / Rejected, tối đa 1000 ký tự.</summary>
    public string? AdminNote { get; set; }
}
