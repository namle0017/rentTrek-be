using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>SQL thuần (bảng không có entity): upsert nguyên tử nên nhiều instance cùng đếm không mất lần sai.</summary>
public sealed class AuthAttemptRepository(AppDbContext context) : IAuthAttemptRepository
{
    public async Task<int> GetFailuresAsync(string key, DateTimeOffset windowCutoff, CancellationToken cancellationToken)
        => (await context.Database.SqlQuery<int>($"""
            SELECT failures AS "Value" FROM public.auth_attempts
            WHERE attempt_key = {key} AND window_start > {windowCutoff}
            """).ToListAsync(cancellationToken)).FirstOrDefault();

    public async Task<int> RecordFailureAsync(string key, DateTimeOffset now, DateTimeOffset windowCutoff, CancellationToken cancellationToken)
    {
        // Dọn dòng hết hạn để bảng không phình khi bị thử hàng loạt email.
        await context.Database.ExecuteSqlAsync(
            $"DELETE FROM public.auth_attempts WHERE window_start <= {windowCutoff} AND attempt_key <> {key}", cancellationToken);
        return (await context.Database.SqlQuery<int>($"""
            INSERT INTO public.auth_attempts AS a (attempt_key, failures, window_start)
            VALUES ({key}, 1, {now})
            ON CONFLICT (attempt_key) DO UPDATE SET
                failures = CASE WHEN a.window_start <= {windowCutoff} THEN 1 ELSE a.failures + 1 END,
                window_start = CASE WHEN a.window_start <= {windowCutoff} THEN {now} ELSE a.window_start END
            RETURNING failures AS "Value"
            """).ToListAsync(cancellationToken)).Single();
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken)
        => context.Database.ExecuteSqlAsync($"DELETE FROM public.auth_attempts WHERE attempt_key = {key}", cancellationToken);
}
