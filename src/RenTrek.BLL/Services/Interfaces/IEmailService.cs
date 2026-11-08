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
}
