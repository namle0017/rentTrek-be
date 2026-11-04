namespace RenTrek.BLL.DTOs.RentalOps.Responses;

/// <summary>Tình trạng trễ hạn của thiết bị còn ở khách, tính theo "bây giờ" của server.</summary>
public sealed class AdminRentalOverdueResponse
{
    public bool IsOverdue { get; set; }
    public int OverdueHours { get; set; }
    public int LateDays { get; set; }
    public decimal GraceHours { get; set; }

    /// <summary>Phí trễ hệ thống sẽ đề xuất nếu nhận trả toàn bộ thiết bị còn lại ngay bây giờ.</summary>
    public decimal SuggestedLateFee { get; set; }
}
