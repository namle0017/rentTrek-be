namespace RenTrek.DAL.Entities;

/// <summary>
/// Ảnh chứng từ chuyển khoản hoàn tiền (lưu ở storage riêng tư). Gắn với đúng một hồ sơ hoàn tiền hoặc một khoản hoàn (payment).
/// </summary>
public sealed class RefundProof
{
    public Guid RefundProofId { get; set; }
    public Guid? RefundCaseId { get; set; }
    public Guid? PaymentId { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }

    /// <summary>ExcelImport | Manual.</summary>
    public string Source { get; set; } = "Manual";

    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public RefundCase? RefundCase { get; set; }
}
