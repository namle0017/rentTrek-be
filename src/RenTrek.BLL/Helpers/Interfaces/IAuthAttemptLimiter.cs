namespace RenTrek.BLL.Helpers.Interfaces;

public interface IAuthAttemptLimiter
{
    Task<bool> IsLockedAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Ghi nhận 1 lần sai; trả true nếu khóa vừa bị kích hoạt hoặc đang bị khóa.</summary>
    Task<bool> RecordFailureAsync(string key, CancellationToken cancellationToken = default);

    Task ResetAsync(string key, CancellationToken cancellationToken = default);
}
