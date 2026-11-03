namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// Admin từ chối hồ sơ hoặc ghi nhận chuyển khoản thất bại.
/// </summary>
public sealed class RefundDecisionRequest
{
    public string Reason { get; set; } = string.Empty;
    public int ExpectedRowVersion { get; set; }
}
