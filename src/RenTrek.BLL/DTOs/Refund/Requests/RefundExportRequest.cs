namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// OTP của Admin để xuất danh sách hoàn tiền cho kế toán.
/// </summary>
public sealed class RefundExportRequest
{
    public Guid ChallengeId { get; set; }
    public string VerificationCode { get; set; } = string.Empty;

    /// <summary>Xuất lại hồ sơ đã gửi kế toán (khi mất file) thay vì hồ sơ mới.</summary>
    public bool ReExport { get; set; }

    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
}
