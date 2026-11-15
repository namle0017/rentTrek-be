namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>Báo cáo nhập file kết quả chuyển khoản của kế toán (xem trước hoặc đã áp dụng).</summary>
public sealed class RefundImportResultResponse
{
    /// <summary>Null khi chỉ xem trước (dryRun).</summary>
    public Guid? BatchId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<RefundImportRowResponse> Rows { get; set; } = [];
}
