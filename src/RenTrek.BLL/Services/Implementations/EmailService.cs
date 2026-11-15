using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.BLL.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger) : this(Microsoft.Extensions.Options.Options.Create(new EmailSettings()), logger)
    {
    }

    public EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetOtpAsync(
        string toEmail,
        string recipientName,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var subject = "[renTrek] Mã xác thực khôi phục mật khẩu của bạn";
        var htmlBody = GenerateHtmlTemplate(
            title: "Khôi phục mật khẩu renTrek",
            recipientName: recipientName,
            description: "Chúng tôi nhận được yêu cầu khôi phục mật khẩu cho tài khoản renTrek của bạn. Vui lòng nhập mã OTP dưới đây để tiến hành đặt lại mật khẩu mới:",
            otp: otp,
            note: "Mã OTP có hiệu lực trong 15 phút. Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này hoặc liên hệ ngay với bộ phận hỗ trợ renTrek.",
            accentColor: "#E76F51");

        await SendEmailOrDevFallbackAsync(toEmail, recipientName, subject, htmlBody, otp, "PASSWORD RESET OTP", cancellationToken);
    }

    public async Task SendEmailVerificationOtpAsync(
        string toEmail,
        string recipientName,
        string otp,
        string? verifyUrl = null,
        CancellationToken cancellationToken = default)
    {
        var subject = "[renTrek] Xác thực và kích hoạt tài khoản renTrek";
        var verifyButtonHtml = string.IsNullOrWhiteSpace(verifyUrl) ? "" : $"""
            <div style="text-align: center; margin: 20px 0;">
              <a href="{WebUtility.HtmlEncode(verifyUrl)}" style="display: inline-block; background-color: #1B4332; color: #ffffff; padding: 12px 28px; font-weight: 600; text-decoration: none; border-radius: 8px;">
                Bấm vào đây để xác thực ngay
              </a>
            </div>
            """;

        var htmlBody = GenerateHtmlTemplate(
            title: "Kích hoạt tài khoản renTrek",
            recipientName: recipientName,
            description: $"Chào mừng bạn đến với renTrek! Để kích hoạt tài khoản, bạn có thể nhập mã OTP 6 số bên dưới trên giao diện hoặc bấm trực tiếp vào liên kết xác thực:{verifyButtonHtml}",
            otp: otp,
            note: "Mã OTP và liên kết xác thực có hiệu lực trong 15 phút. Tuyệt đối không chia sẻ mã này cho bất kỳ ai.",
            accentColor: "#1B4332");

        await SendEmailOrDevFallbackAsync(toEmail, recipientName, subject, htmlBody, otp, "EMAIL VERIFICATION OTP", cancellationToken);
    }

    public async Task SendChangePasswordOtpAsync(
        string toEmail,
        string recipientName,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var subject = "[renTrek] Mã xác thực yêu cầu đổi mật khẩu tài khoản";
        var htmlBody = GenerateHtmlTemplate(
            title: "Xác thực đổi mật khẩu renTrek",
            recipientName: recipientName,
            description: "Chúng tôi nhận được yêu cầu đổi mật khẩu cho tài khoản renTrek của bạn. Vui lòng nhập mã OTP dưới đây để hoàn tất việc cập nhật mật khẩu mới:",
            otp: otp,
            note: "Mã OTP có hiệu lực trong 15 phút. Nếu bạn không thực hiện yêu cầu này, vui lòng đổi mật khẩu ngay hoặc liên hệ bộ phận hỗ trợ của renTrek.",
            accentColor: "#2A9D8F");

        await SendEmailOrDevFallbackAsync(toEmail, recipientName, subject, htmlBody, otp, "CHANGE PASSWORD OTP", cancellationToken);
    }

    public async Task SendRentalOrderConfirmationAsync(
        string toEmail,
        string recipientName,
        string orderCode,
        decimal totalDeposit,
        decimal totalRentalFee,
        string rentalDates,
        CancellationToken cancellationToken = default)
    {
        var subject = $"[renTrek] Xác nhận đơn thuê thiết bị #{orderCode}";
        var totalAmount = totalDeposit + totalRentalFee;
        var htmlBody = GenerateOrderNotificationHtml(
            title: "Xác nhận đặt thuê thiết bị thành công",
            recipientName: recipientName,
            orderCode: orderCode,
            orderTypeLabel: "Đơn thuê thiết bị dã ngoại",
            detailRows: new[]
            {
                ("Mã đơn hàng", $"<strong>#{orderCode}</strong>"),
                ("Thời gian thuê", rentalDates),
                ("Tiền thuê thiết bị", $"{totalRentalFee:N0} đ"),
                ("Tiền cọc thiết bị", $"{totalDeposit:N0} đ"),
                ("Tổng thanh toán", $"<strong style=\"color: #1B4332; font-size: 16px;\">{totalAmount:N0} đ</strong>")
            },
            instruction: "Quý khách vui lòng đến cửa hàng renTrek đúng thời gian đã đăng ký để nhận và kiểm tra thiết bị. Sau khi trả đồ và nhân viên kiểm tra, phần cọc còn lại (sau khi trừ phí phát sinh nếu có) sẽ được hoàn: hệ thống sẽ gửi yêu cầu nhập số tài khoản nhận tiền.",
            accentColor: "#1B4332");

        await SendEmailOrDevFallbackAsync(toEmail, recipientName, subject, htmlBody, null, "RENTAL ORDER CONFIRMATION", cancellationToken);
    }

    public async Task SendRentalNoticeAsync(
        string toEmail,
        string recipientName,
        string orderCode,
        string subject,
        string title,
        IReadOnlyList<(string Label, string Value)> rows,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        var htmlBody = GenerateOrderNotificationHtml(
            title: title,
            recipientName: recipientName,
            orderCode: orderCode,
            orderTypeLabel: "Đơn thuê thiết bị dã ngoại",
            detailRows: rows.ToArray(),
            instruction: instruction,
            accentColor: "#1B4332");

        await SendEmailOrDevFallbackAsync(toEmail, recipientName, $"[renTrek] {subject} #{orderCode}", htmlBody, null, "RENTAL NOTICE", cancellationToken);
    }

    public async Task SendTourBookingConfirmationAsync(
        string toEmail,
        string recipientName,
        string orderCode,
        string tourName,
        string tripDate,
        decimal totalAmount,
        int participantCount,
        CancellationToken cancellationToken = default)
    {
        var subject = $"[renTrek] Xác nhận đặt tour trekking #{orderCode}";
        var htmlBody = GenerateOrderNotificationHtml(
            title: "Xác nhận đặt vé tour trekking thành công",
            recipientName: recipientName,
            orderCode: orderCode,
            orderTypeLabel: "Vé tour trekking dã ngoại",
            detailRows: new[]
            {
                ("Mã đặt chỗ", $"<strong>#{orderCode}</strong>"),
                ("Tên tour", tourName),
                ("Ngày khởi hành", tripDate),
                ("Số người tham gia", $"{participantCount} người"),
                ("Tổng tiền vé tour", $"<strong style=\"color: #1B4332; font-size: 16px;\">{totalAmount:N0} đ</strong>")
            },
            instruction: null,
            accentColor: "#264653");

        await SendEmailOrDevFallbackAsync(toEmail, recipientName, subject, htmlBody, null, "TOUR BOOKING CONFIRMATION", cancellationToken);
    }

    private async Task SendEmailOrDevFallbackAsync(
        string toEmail,
        string recipientName,
        string subject,
        string htmlBody,
        string? otp,
        string devTag,
        CancellationToken cancellationToken)
    {
        // 1. Nếu chưa cấu hình SMTP Host/Username -> Chế độ Development: In console màu nổi bật
        if (!_settings.IsConfigured)
        {
            var otpSnippet = string.IsNullOrWhiteSpace(otp)
                ? "(Không có mã OTP - Thông báo xác nhận đơn)"
                : $"MÃ OTP (6 CHỮ SỐ): [ {otp} ]";

            var devBox = $"""
                ================================================================================
                [renTrek Email Mock / Dev] {devTag}
                Gửi tới: {recipientName} <{toEmail}>
                Tiêu đề: {subject}
                Thời gian: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                {otpSnippet}
                (Lưu ý: Cấu hình section 'Email' trong .env để gửi mail SMTP thật)
                ================================================================================
                """;
            _logger.LogInformation("{DevBox}", devBox);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(devBox);
            Console.ResetColor();
            return;
        }

        // 2. Chế độ Production: Gửi email thực tế qua MailKit SMTP
        try
        {
            using var message = new MimeMessage();
            var fromAddr = string.IsNullOrWhiteSpace(_settings.FromAddress) ? _settings.Username : _settings.FromAddress;
            var fromName = string.IsNullOrWhiteSpace(_settings.FromName) ? "renTrek Outdoor" : _settings.FromName;

            message.From.Add(new MailboxAddress(fromName, fromAddr));
            message.To.Add(new MailboxAddress(recipientName, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = string.IsNullOrWhiteSpace(otp)
                    ? $"{subject}. Cảm ơn quý khách đã sử dụng dịch vụ của renTrek."
                    : $"Mã xác thực renTrek của bạn là: {otp} (Hiệu lực 15 phút)."
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            var host = string.IsNullOrWhiteSpace(_settings.SmtpHost) ? "smtp.gmail.com" : _settings.SmtpHost;
            var port = _settings.SmtpPort > 0 ? _settings.SmtpPort : 587;

            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Đã gửi email '{Subject}' tới {ToEmail} thành công qua MailKit SMTP", subject, toEmail);
        }
        catch (Exception ex)
        {
            // Không ghi OTP vào log production
            _logger.LogError(ex, "Gửi email qua MailKit thất bại tới {ToEmail}. Thẻ: {DevTag}", toEmail, devTag);
        }
    }

    private static string GenerateOrderNotificationHtml(
        string title,
        string recipientName,
        string orderCode,
        string orderTypeLabel,
        (string Label, string Value)[] detailRows,
        string? instruction,
        string accentColor)
    {
        var rowsHtml = string.Join("\n", detailRows.Select(row => $"""
            <tr>
              <td style="padding: 10px 14px; border-bottom: 1px solid #edf2f7; color: #718096; font-size: 14px; width: 40%;">{row.Label}</td>
              <td style="padding: 10px 14px; border-bottom: 1px solid #edf2f7; color: #1a202c; font-size: 14px; text-align: right;">{row.Value}</td>
            </tr>
            """));

        var instructionHtml = string.IsNullOrWhiteSpace(instruction)
            ? string.Empty
            : $"""
                          <div style="background-color: #ebf8ff; border-left: 4px solid #3182ce; padding: 14px 16px; border-radius: 4px; font-size: 13px; color: #2b6cb0; line-height: 1.5; margin-bottom: 24px;">
                            <strong>Hướng dẫn tiếp theo:</strong> {instruction}
                          </div>
              """;

        return $"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>{title}</title>
            </head>
            <body style="margin: 0; padding: 0; background-color: #f4f6f8; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #2d3748;">
              <table border="0" cellpadding="0" cellspacing="0" width="100%" style="table-layout: fixed; background-color: #f4f6f8; padding: 40px 10px;">
                <tr>
                  <td align="center">
                    <table border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width: 580px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.06);">
                      <!-- Header -->
                      <tr>
                        <td align="center" style="background-color: {accentColor}; padding: 32px 20px;">
                          <div style="font-size: 26px; font-weight: 800; color: #ffffff; letter-spacing: -0.5px;">
                            <span style="color: #52B788;">▲</span> renTrek
                          </div>
                          <div style="font-size: 13px; color: #D8F3DC; margin-top: 6px; text-transform: uppercase; letter-spacing: 1px;">
                            {orderTypeLabel}
                          </div>
                        </td>
                      </tr>
                      <!-- Body -->
                      <tr>
                        <td style="padding: 36px 32px;">
                          <h2 style="margin: 0 0 16px 0; color: #1a202c; font-size: 20px; font-weight: 700;">{title}</h2>
                          <p style="margin: 0 0 16px 0; font-size: 15px; line-height: 1.6; color: #4a5568;">
                            Kính chào <strong>{recipientName}</strong>,
                          </p>
                          <div style="margin: 0 0 24px 0; font-size: 15px; line-height: 1.6; color: #4a5568;">
                            Cảm ơn bạn đã lựa chọn dịch vụ của <strong>renTrek</strong>. Đơn hàng của bạn đã được ghi nhận thành công trên hệ thống với thông tin chi tiết dưới đây:
                          </div>
                          
                          <!-- Order Details Table -->
                          <div style="background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; overflow: hidden; margin: 24px 0;">
                            <table border="0" cellpadding="0" cellspacing="0" width="100%" style="border-collapse: collapse;">
                              {rowsHtml}
                            </table>
                          </div>

                          {instructionHtml}

                          <p style="margin: 0; font-size: 14px; line-height: 1.6; color: #718096;">
                            Nếu cần hỗ trợ thêm thông tin, bạn có thể xem lại tại mục <strong>Đơn của tôi</strong> trên website renTrek hoặc liên hệ hotline chăm sóc khách hàng của chúng tôi.
                          </p>
                        </td>
                      </tr>
                      <!-- Footer -->
                      <tr>
                        <td align="center" style="background-color: #f7fafc; padding: 20px; border-top: 1px solid #edf2f7; font-size: 12px; color: #a0aec0;">
                          © {DateTime.UtcNow.Year} renTrek Inc. Nền tảng thuê trang bị dã ngoại & tour trekking ký quỹ.<br>
                          Email này được gửi tự động để xác nhận giao dịch của bạn tại renTrek.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string GenerateHtmlTemplate(
        string title,
        string recipientName,
        string description,
        string otp,
        string note,
        string accentColor)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>{title}</title>
            </head>
            <body style="margin: 0; padding: 0; background-color: #f4f6f8; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #2d3748;">
              <table border="0" cellpadding="0" cellspacing="0" width="100%" style="table-layout: fixed; background-color: #f4f6f8; padding: 40px 10px;">
                <tr>
                  <td align="center">
                    <table border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width: 580px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.06);">
                      <!-- Header -->
                      <tr>
                        <td align="center" style="background-color: #1B4332; padding: 32px 20px;">
                          <div style="font-size: 26px; font-weight: 800; color: #ffffff; letter-spacing: -0.5px;">
                            <span style="color: #52B788;">▲</span> renTrek
                          </div>
                          <div style="font-size: 13px; color: #D8F3DC; margin-top: 6px; text-transform: uppercase; letter-spacing: 1px;">
                            Outdoor Adventure & Gear Escrow
                          </div>
                        </td>
                      </tr>
                      <!-- Body -->
                      <tr>
                        <td style="padding: 36px 32px;">
                          <h2 style="margin: 0 0 16px 0; color: #1a202c; font-size: 20px; font-weight: 700;">{title}</h2>
                          <p style="margin: 0 0 16px 0; font-size: 15px; line-height: 1.6; color: #4a5568;">
                            Xin chào <strong>{WebUtility.HtmlEncode(recipientName)}</strong>,
                          </p>
                          <div style="margin: 0 0 24px 0; font-size: 15px; line-height: 1.6; color: #4a5568;">
                            {description}
                          </div>
                          
                          <!-- OTP Box -->
                          <div style="background-color: #f8fafc; border: 2px dashed {accentColor}; border-radius: 10px; padding: 24px; text-align: center; margin: 28px 0;">
                            <div style="font-size: 12px; text-transform: uppercase; letter-spacing: 1.5px; color: #718096; margin-bottom: 8px;">
                              MÃ XÁC THỰC OTP CỦA BẠN
                            </div>
                            <div style="font-size: 34px; font-weight: 800; letter-spacing: 8px; color: {accentColor}; font-family: monospace;">
                              {otp}
                            </div>
                            <div style="font-size: 13px; color: #e53e3e; margin-top: 8px; font-weight: 600;">
                              Hiệu lực trong 15 phút
                            </div>
                          </div>

                          <div style="background-color: #fefcbf; border-left: 4px solid #d69e2e; padding: 12px 16px; border-radius: 4px; font-size: 13px; color: #744210; line-height: 1.5; margin-bottom: 24px;">
                            <strong>Lưu ý an toàn:</strong> {note}
                          </div>

                          <p style="margin: 0; font-size: 14px; line-height: 1.6; color: #718096;">
                            Nếu bạn cần trợ giúp, xin vui lòng phản hồi email này hoặc liên hệ hotline hỗ trợ của renTrek.
                          </p>
                        </td>
                      </tr>
                      <!-- Footer -->
                      <tr>
                        <td align="center" style="background-color: #f7fafc; padding: 20px; border-top: 1px solid #edf2f7; font-size: 12px; color: #a0aec0;">
                          © {DateTime.UtcNow.Year} renTrek Inc. Nền tảng thuê trang bị dã ngoại & trekking uy tín hàng đầu.<br>
                          Email này được gửi tự động từ hệ thống bảo mật renTrek.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
