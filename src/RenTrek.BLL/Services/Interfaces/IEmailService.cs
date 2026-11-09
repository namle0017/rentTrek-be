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
