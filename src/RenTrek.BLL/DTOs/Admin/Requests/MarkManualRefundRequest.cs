namespace RenTrek.BLL.DTOs.Admin.Requests;

/// <summary>
/// Ghi nhận kế toán đã chuyển khoản hoàn tiền ngoài hệ thống.
/// </summary>
/// <param name="ManualReference">Mã tham chiếu giao dịch ngân hàng (bắt buộc, không trùng giao dịch khác).</param>
/// <param name="Note">Ghi chú lưu vào audit log.</param>
public sealed record MarkManualRefundRequest(string ManualReference, string? Note);
