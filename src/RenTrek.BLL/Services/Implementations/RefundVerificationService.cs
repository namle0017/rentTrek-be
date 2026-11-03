using System.Security.Cryptography;
using RenTrek.BLL.Constants;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Quản lý OTP email cho thao tác xuất danh sách hoàn tiền của Admin.
/// </summary>
public sealed class RefundVerificationService(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : IRefundVerificationService
{
    private const int MaxAttempts = 5;
    private const int MaxIssuesPerWindow = 3;
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IssueWindow = TimeSpan.FromMinutes(15);

    public async Task<(RefundActionChallenge Challenge, string Code)> IssueAsync(
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        var issued = await unitOfWork.RefundCases.CountChallengesSinceAsync(
            adminUserId,
            utcNow - IssueWindow,
            cancellationToken);
        if (issued >= MaxIssuesPerWindow)
            throw new TooManyRequestsException("Đã gửi quá 3 mã trong 15 phút, vui lòng thử lại sau", ErrorCodes.TooManyRequests);

        foreach (var open in await unitOfWork.RefundCases.GetOpenChallengesAsync(adminUserId, cancellationToken))
            open.UsedAt = utcNow;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = new RefundActionChallenge
        {
            ChallengeId = Guid.NewGuid(),
            AdminUserId = adminUserId,
            CodeHash = passwordHasher.Hash(code),
            ExpiresAt = utcNow + CodeLifetime,
            CreatedAt = utcNow
        };
        await unitOfWork.RefundCases.AddChallengeAsync(challenge, cancellationToken);
        return (challenge, code);
    }

    public async Task<string?> VerifyAsync(
        Guid adminUserId,
        Guid challengeId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var challenge = await unitOfWork.RefundCases.GetChallengeAsync(challengeId, cancellationToken);
        if (challenge is null ||
            challenge.AdminUserId != adminUserId ||
            challenge.UsedAt is not null)
        {
            return ErrorCodes.RefundVerificationInvalid;
        }

        var utcNow = timeProvider.GetUtcNow();
        if (challenge.ExpiresAt <= utcNow || challenge.AttemptCount >= MaxAttempts)
            return ErrorCodes.RefundVerificationExpired;

        challenge.AttemptCount++;
        if (!passwordHasher.Verify(code, challenge.CodeHash))
            return ErrorCodes.RefundVerificationInvalid;

        challenge.UsedAt = utcNow;
        return null;
    }
}
