using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Admin;

public sealed partial class AdminService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : IAdminService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly TimeProvider _timeProvider = timeProvider;
    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;
    private Guid ActorId => _currentUser.RequiredUserId;
    private bool IsAdmin => _currentUser.Role == RoleNames.Admin;

    private void RequireAdmin()
    {
        if (!IsAdmin) throw new ForbiddenException("Chỉ quản trị viên được thực hiện thao tác này");
    }

    private static void Check(bool condition, string field, string message)
    {
        if (!condition) throw new ValidationException(new Dictionary<string, string[]> { [field] = [message] });
    }

    private static string Required(string? value, string field, int maxLength = 200)
    {
        var trimmed = value?.Trim();
        Check(!string.IsNullOrEmpty(trimmed) && trimmed.Length <= maxLength, field,
            $"Trường bắt buộc, tối đa {maxLength} ký tự");
        return trimmed!;
    }

    private static string Choice(string? value, string field, params string[] choices)
    {
        Check(value != null && choices.Contains(value), field, "Giá trị không hợp lệ");
        return value!;
    }

    private async Task AtomicAsync(string key, Func<Task> action, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _unitOfWork.AdminOperations.AcquireLockAsync(key, cancellationToken);
            await action();
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private Task AuditAsync(string action, string entityType, Guid entityId, object details,
        CancellationToken cancellationToken)
        => _unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            AuditLogId = Guid.NewGuid(), ActorUserId = ActorId, Action = action,
            EntityType = entityType, EntityId = entityId, CreatedAt = Now,
            Details = JsonSerializer.Serialize(details, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, cancellationToken);

    private static JsonElement AuditDetails(string details)
    {
        try { return JsonSerializer.Deserialize<JsonElement>(details); }
        catch (JsonException) { return JsonSerializer.SerializeToElement(details); }
    }

    private static async Task<PagedResponse<TResponse>> PageAsync<TEntity, TResponse>(
        IQueryable<TEntity> query, AdminQueryRequest request, Expression<Func<TEntity, TResponse>> projection,
        CancellationToken cancellationToken, string defaultSort = "CreatedAt", params string[] allowedSorts)
    {
        Check(request.PageNumber >= 1 && request.PageSize is >= 1 and <= 100, "pageNumber", "Phân trang không hợp lệ");
        Check(request.PageNumber <= int.MaxValue / request.PageSize, "pageNumber", "Số trang vượt giới hạn");
        var property = typeof(TEntity).GetProperties().FirstOrDefault(x =>
            x.Name.Equals(request.SortBy, StringComparison.OrdinalIgnoreCase) &&
            (x.Name == defaultSort || allowedSorts.Contains(x.Name)))
            ?? typeof(TEntity).GetProperty(defaultSort)!;
        var parameter = Expression.Parameter(typeof(TEntity), "row");
        var selector = Expression.Lambda(Expression.Property(parameter, property), parameter);
        var sorted = query.Provider.CreateQuery<TEntity>(Expression.Call(typeof(Queryable),
            request.SortOrder == "asc" ? "OrderBy" : "OrderByDescending",
            [typeof(TEntity), property.PropertyType], query.Expression, Expression.Quote(selector)));
        var key = typeof(TEntity).GetProperty("Id") ?? typeof(TEntity).GetProperty(typeof(TEntity).Name + "Id")
            ?? typeof(TEntity).GetProperties().First(x => x.Name.EndsWith("Id", StringComparison.Ordinal));
        var keySelector = Expression.Lambda(Expression.Property(parameter, key), parameter);
        sorted = sorted.Provider.CreateQuery<TEntity>(Expression.Call(typeof(Queryable), "ThenBy",
            [typeof(TEntity), key.PropertyType], sorted.Expression, Expression.Quote(keySelector)));
        return new PagedResponse<TResponse>
        {
            TotalItems = await query.CountAsync(cancellationToken), PageNumber = request.PageNumber,
            PageSize = request.PageSize, Items = await sorted.Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize).Select(projection).ToListAsync(cancellationToken)
        };
    }
}
