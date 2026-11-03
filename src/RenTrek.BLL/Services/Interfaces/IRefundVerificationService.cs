using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Interfaces;

public interface IRefundVerificationService
{
    /// <summary>Phát hành OTP mới và vô hiệu mã cũ. Trả challenge cùng mã thô để caller gửi email sau khi commit.</summary>
    Task<(RefundActionChallenge Challenge, string Code)> IssueAsync(
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trả <c>null</c> nếu mã đúng (challenge bị đánh dấu đã dùng), ngược lại trả mã lỗi.
    /// Không ném lỗi để caller commit được <c>attempt_count</c> trước khi báo lỗi.
    /// </summary>
    Task<string?> VerifyAsync(
        Guid adminUserId,
        Guid challengeId,
        string code,
        CancellationToken cancellationToken = default);
}
