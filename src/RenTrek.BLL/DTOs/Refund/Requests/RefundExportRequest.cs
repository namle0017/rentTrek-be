namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// OTP của Admin để xuất danh sách hoàn tiền cho kế toán.
/// </summary>
public sealed class RefundExportRequest
{
    public Guid ChallengeId { get; set; }
    public string VerificationCode { get; set; } = string.Empty;

    /// <summary>Không còn dùng: mỗi lần xuất luôn gồm cả hồ sơ mới và hồ sơ đã gửi kế toán chưa có kết quả. Giữ để tương thích client cũ.</summary>
    public bool ReExport { get; set; }

    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
}
