using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using RenTrek.DAL.Data;

namespace RenTrek.DAL.Extensions;

/// <summary>
/// Khoá advisory theo transaction của PostgreSQL, dùng chung cho các repository cần tuần tự hoá nghiệp vụ.
/// </summary>
internal static class PostgresAdvisoryLock
{
    /// <summary>
    /// Lấy <c>pg_advisory_xact_lock(hashtextextended(key, 0))</c>; khoá tự nhả khi commit / rollback.
    /// BẮT BUỘC đã mở transaction (IUnitOfWork.BeginTransactionAsync).
    /// </summary>
    public static async Task AcquireAsync(AppDbContext context, string lockKey, CancellationToken cancellationToken)
    {
        var transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("PostgreSQL advisory lock requires an active transaction");
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@lock_key, 0));";
        command.Parameters.AddWithValue("lock_key", lockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
