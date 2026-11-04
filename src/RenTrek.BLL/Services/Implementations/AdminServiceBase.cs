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

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Hạ tầng dùng chung cho các service Admin: actor, transaction có khóa, audit log, phân trang.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public abstract class AdminServiceBase(IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
{
    protected readonly IUnitOfWork _unitOfWork = unitOfWork;

    protected readonly ICurrentUserService _currentUser = currentUser;

    protected readonly TimeProvider _timeProvider = timeProvider;

    protected DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    protected Guid ActorId => _currentUser.RequiredUserId;

    protected bool IsAdmin => _currentUser.Role == RoleNames.Admin;

    protected void RequireAdmin()
    {
        if (!IsAdmin) throw new ForbiddenException("Chỉ quản trị viên được thực hiện thao tác này");
    }

    protected static void Check(bool condition, string field, string message)
    {
        if (!condition) throw new ValidationException(new Dictionary<string, string[]> { [field] = [message] });
    }

    protected static string Required(string? value, string field, int maxLength = 200)
    {
        var trimmed = value?.Trim();
        Check(!string.IsNullOrEmpty(trimmed) && trimmed.Length <= maxLength, field,
            $"Trường bắt buộc, tối đa {maxLength} ký tự");
        return trimmed!;
    }

    protected async Task AtomicAsync(string key, Func<Task> action, CancellationToken cancellationToken)
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

    protected Task AuditAsync(string action, string entityType, Guid entityId, object details,
        CancellationToken cancellationToken)
        => _unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            ActorUserId = ActorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            CreatedAt = Now,
            Details = JsonSerializer.Serialize(details, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }, cancellationToken);

    protected static JsonElement AuditDetails(string details)
    {
        try { return JsonSerializer.Deserialize<JsonElement>(details); }
        catch (JsonException) { return JsonSerializer.SerializeToElement(details); }
    }

    protected static async Task<PagedResponse<TResponse>> PageAsync<TEntity, TResponse>(
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
            TotalItems = await query.CountAsync(cancellationToken),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Items = await sorted.Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize).Select(projection).ToListAsync(cancellationToken)
        };
    }

    protected IQueryable<Order> ScopedOrders(bool tracking = false)
    {
        var query = tracking ? _unitOfWork.Orders.QueryTracking() : _unitOfWork.Orders.Query();
        return IsAdmin ? query : query.Where(x =>
            (x.OrderType == OrderTypes.TripBooking && x.TripBookingOrder.Trip.ManagedByUserId == ActorId) ||
            (x.OrderType == OrderTypes.Rental && x.RentalOrder != null && x.RentalOrder.HandledByUserId == ActorId));
    }
}
