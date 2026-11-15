namespace RenTrek.BLL.DTOs.Refund.Internal;

/// <summary>Một file ảnh chứng từ do Admin tải lên (đã mở stream từ multipart).</summary>
public sealed record ProofFileInput(Stream Content, string FileName, string ContentType, long Length);
