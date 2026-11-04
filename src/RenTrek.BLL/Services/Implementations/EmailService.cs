using Microsoft.Extensions.Logging;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.BLL.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetOtpAsync(
        string toEmail,
        string recipientName,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var message = $"""
            ================================================================================
            [renTrek Security] MÃ XÁC THỰC KHÔI PHỤC MẬT KHẨU (PASSWORD RESET OTP)
            Gửi tới: {recipientName} <{toEmail}>
            Thời gian: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
            Mã OTP (6 chữ số): [ {otp} ]
            Hiệu lực: 15 phút. Tuyệt đối không chia sẻ mã này với bất kỳ ai!
            ================================================================================
            """;

        _logger.LogInformation("{Message}", message);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(message);
        Console.ResetColor();

        return Task.CompletedTask;
    }
}
