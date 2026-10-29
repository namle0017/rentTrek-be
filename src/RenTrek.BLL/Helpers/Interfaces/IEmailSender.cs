namespace RenTrek.BLL.Helpers.Interfaces;

public interface IEmailSender
{
    Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
