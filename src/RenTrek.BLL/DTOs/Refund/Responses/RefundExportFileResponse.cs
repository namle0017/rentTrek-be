namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// File Excel gửi kế toán; chứa số tài khoản rõ nên không được lưu lại ở server.
/// </summary>
public sealed record RefundExportFileResponse(string FileName, byte[] Content);
