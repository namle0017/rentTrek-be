using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Khóa tạm theo khóa (vd "login:email") sau <see cref="MaxFailures"/> lần sai trong <see cref="LockDuration"/> (PRD NFR-02).
/// Bộ đếm lưu ở DB (<see cref="IAuthAttemptRepository"/>) nên đúng cả khi chạy nhiều instance.
/// </summary>
public sealed class AuthAttemptLimiter(IAuthAttemptRepository attempts, TimeProvider timeProvider) : IAuthAttemptLimiter
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    public async Task<bool> IsLockedAsync(string key, CancellationToken cancellationToken = default)
        => await attempts.GetFailuresAsync(key, Cutoff(timeProvider.GetUtcNow()), cancellationToken) >= MaxFailures;

    /// <inheritdoc />
    public async Task<bool> RecordFailureAsync(string key, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return await attempts.RecordFailureAsync(key, now, Cutoff(now), cancellationToken) >= MaxFailures;
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken = default)
        => attempts.ResetAsync(key, cancellationToken);

    private static DateTimeOffset Cutoff(DateTimeOffset now) => now - LockDuration;
}
