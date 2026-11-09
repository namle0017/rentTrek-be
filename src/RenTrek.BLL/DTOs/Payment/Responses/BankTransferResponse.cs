namespace RenTrek.BLL.DTOs.Payment.Responses;

/// <summary>
/// Hướng dẫn chuyển khoản VietQR cho một lần thanh toán.
/// </summary>
public sealed class BankTransferResponse
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Nội dung chuyển khoản bắt buộc giữ nguyên để hệ thống tự đối soát.</summary>
    public string TransferContent { get; set; } = string.Empty;
    public string? QrPayload { get; set; }
    public string? QrImageUrl { get; set; }
}
