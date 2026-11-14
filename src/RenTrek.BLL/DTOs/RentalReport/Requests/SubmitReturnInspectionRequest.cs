using RenTrek.BLL.DTOs.RentalOrder.Responses;

namespace RenTrek.BLL.DTOs.RentalReport.Requests;

/// <summary>Staff gửi báo cáo kiểm tra trả đồ cho admin.</summary>
public class SubmitReturnInspectionRequest
{
    public string? Note { get; set; }
}
