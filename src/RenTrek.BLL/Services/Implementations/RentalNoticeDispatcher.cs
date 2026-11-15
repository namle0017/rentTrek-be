using Microsoft.Extensions.Logging;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <inheritdoc cref="IRentalNoticeDispatcher"/>
public sealed class RentalNoticeDispatcher(
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    IEmailService emailService,
    ILogger<RentalNoticeDispatcher> logger) : IRentalNoticeDispatcher
{
    private readonly List<(Guid RecipientUserId, string OrderCode, RentalEmailNotice Email)> _pending = [];
    private readonly HashSet<(Guid, string)> _queuedKeys = [];

    public async Task<bool> QueueAsync(
        Guid recipientUserId,
        string orderCode,
        Guid orderId,
        string type,
        string title,
        string content,
        string eventKey,
        RentalEmailNotice? email,
        CancellationToken cancellationToken = default)
    {
        // Khử trùng cả trong cùng transaction (chưa SaveChanges nên DB chưa thấy) lẫn với lần chạy trước
        if (!_queuedKeys.Add((recipientUserId, eventKey))
            || await unitOfWork.Notifications.ExistsByEventKeyAsync(recipientUserId, eventKey, cancellationToken))
        {
            return false;
        }

        await notificationService.QueueAsync(recipientUserId, type, title, content, "Order", orderId, eventKey, cancellationToken);
        if (email is not null)
            _pending.Add((recipientUserId, orderCode, email));
        return true;
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var batch = _pending.ToList();
        _pending.Clear();
        _queuedKeys.Clear();
        foreach (var (recipientUserId, orderCode, email) in batch)
        {
            try
            {
                var account = await unitOfWork.Accounts.GetByIdWithRoleAsync(recipientUserId, false, cancellationToken);
                if (account is null || string.IsNullOrWhiteSpace(account.Email))
                    continue;

                await emailService.SendRentalNoticeAsync(
                    account.Email,
                    account.FullName,
                    orderCode,
                    email.Subject,
                    email.Title,
                    email.Rows,
                    email.Instruction,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Gửi email thông báo đơn thuê {OrderCode} thất bại", orderCode);
            }
        }
    }

    public void Discard()
    {
        _pending.Clear();
        _queuedKeys.Clear();
    }
}
