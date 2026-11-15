namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>Admin/Staff duyệt hồ sơ hoàn tiền (Requested → Approved).</summary>
public sealed class RefundApproveRequest
{
    public int ExpectedRowVersion { get; set; }
    public string? Note { get; set; }
}
