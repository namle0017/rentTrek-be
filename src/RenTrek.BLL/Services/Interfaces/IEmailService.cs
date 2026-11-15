namespace RenTrek.BLL.Services.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetOtpAsync(
        string toEmail,
        string recipientName,
        string otp,
        CancellationToken cancellationToken = default);

    Task SendEmailVerificationOtpAsync(
        string toEmail,
        string recipientName,
        string otp,
        string? verifyUrl = null,
        CancellationToken cancellationToken = default);

    Task SendChangePasswordOtpAsync(
        string toEmail,
        string recipientName,
        string otp,
        CancellationToken cancellationToken = default);

    Task SendRentalOrderConfirmationAsync(
        string toEmail,
        string recipientName,
        string orderCode,
        decimal totalDeposit,
        decimal totalRentalFee,
        string rentalDates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thông báo luồng thuê đồ (phí trễ, không đến nhận, xác nhận nhận hàng, nhắc trả, yêu cầu nhập STK, quyết toán).
    /// Giá trị trong <paramref name="rows"/> là HTML đã an toàn (caller tự encode dữ liệu người dùng nhập).
    /// </summary>
    Task SendRentalNoticeAsync(
        string toEmail,
        string recipientName,
        string orderCode,
        string subject,
        string title,
        IReadOnlyList<(string Label, string Value)> rows,
        string instruction,
        CancellationToken cancellationToken = default);

    Task SendTourBookingConfirmationAsync(
        string toEmail,
        string recipientName,
        string orderCode,
        string tourName,
        string tripDate,
        decimal totalAmount,
        int participantCount,
        CancellationToken cancellationToken = default);
}
