namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>Thông tin một ảnh chứng từ; nội dung tải riêng qua endpoint proofs (có kiểm tra quyền).</summary>
public sealed class RefundProofResponse
{
    public Guid ProofId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
