namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>Ảnh chứng từ đã đọc vào bộ nhớ và xác định loại thật theo magic bytes.</summary>
public sealed record RefundProofFile(byte[] Data, string FileName, string ContentType);
