namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>Kết quả kiểm tra / áp dụng một dòng trong file Excel kết quả chuyển khoản.</summary>
public sealed class RefundImportRowResponse
{
    public int RowNumber { get; set; }
    public Guid? RefundCaseId { get; set; }
    public string RefundCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    /// <summary>Complete | Fail | Skip.</summary>
    public string Action { get; set; } = "Skip";

    public string? TransactionId { get; set; }
    public string? Reason { get; set; }
    public int ImageCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool Applied { get; set; }

    /// <summary>Completed | Failed khi đã áp dụng thành công, ngược lại null.</summary>
    public string? ResultStatus { get; set; }
}
