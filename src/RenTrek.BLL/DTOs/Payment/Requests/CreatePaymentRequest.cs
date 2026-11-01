using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.Payment.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào khi khởi tạo thanh toán cho một đơn.
/// </summary>
public sealed class CreatePaymentRequest
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? IdempotencyKey { get; set; }
    /// <summary>MVP chỉ hỗ trợ <c>BankTransfer</c>.</summary>
    [Required]
    public string PaymentMethod { get; set; } = string.Empty;
}
