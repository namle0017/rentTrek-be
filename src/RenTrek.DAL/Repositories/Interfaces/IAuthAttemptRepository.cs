namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>Bộ đếm lần nhập sai lưu ở bảng <c>auth_attempts</c>, dùng chung mọi instance.</summary>
public interface IAuthAttemptRepository
{
    /// <summary>Số lần sai trong cửa sổ còn hiệu lực (window_start &gt; <paramref name="windowCutoff"/>); 0 nếu không có.</summary>
    Task<int> GetFailuresAsync(string key, DateTimeOffset windowCutoff, CancellationToken cancellationToken);

    /// <summary>Tăng nguyên tử 1 lần sai (mở cửa sổ mới nếu cửa sổ cũ đã hết hạn) và trả số lần sai hiện tại.</summary>
    Task<int> RecordFailureAsync(string key, DateTimeOffset now, DateTimeOffset windowCutoff, CancellationToken cancellationToken);

    Task ResetAsync(string key, CancellationToken cancellationToken);
}
