namespace RenTrek.BLL.DTOs.Payment.Requests;

/// <summary>
/// Payload SePay gửi tới webhook khi tài khoản ngân hàng có biến động số dư.
/// </summary>
public sealed class SePayWebhookRequest
{
    /// <summary>Id giao dịch trên SePay, dùng để chống xử lý lặp.</summary>
    public long Id { get; set; }
    public string? Gateway { get; set; }

    /// <summary>Định dạng <c>yyyy-MM-dd HH:mm:ss</c> theo giờ Việt Nam.</summary>
    public string? TransactionDate { get; set; }
    public string? AccountNumber { get; set; }

    /// <summary>Mã thanh toán SePay tự nhận diện từ nội dung (có thể null).</summary>
    public string? Code { get; set; }
    public string? Content { get; set; }

    /// <summary><c>in</c> là tiền vào, <c>out</c> là tiền ra.</summary>
    public string TransferType { get; set; } = string.Empty;
    public decimal TransferAmount { get; set; }
    public decimal? Accumulated { get; set; }
    public string? SubAccount { get; set; }
    public string? ReferenceCode { get; set; }
    public string? Description { get; set; }
}
