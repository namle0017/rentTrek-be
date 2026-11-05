using System.Text.Json;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
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

    protected static AdminAuditResponse ToAuditResponse(AuditLogRow x)
        => new(x.AuditLogId, x.ActorUserId, x.ActorFullName, x.Action, x.EntityType, x.EntityId,
            AuditDetails(x.Details), x.CreatedAt);

    /// <summary>Kiểm tra tham số phân trang admin.</summary>
    protected static void CheckPaging(AdminQueryRequest request)
    {
        Check(request.PageNumber >= 1 && request.PageSize is >= 1 and <= 100, "pageNumber", "Phân trang không hợp lệ");
        Check(request.PageNumber <= int.MaxValue / request.PageSize, "pageNumber", "Số trang vượt giới hạn");
    }

    /// <summary>Admin chỉ sắp tăng dần khi SortOrder đúng "asc"; mọi giá trị khác là giảm dần.</summary>
    protected static bool IsDescending(AdminQueryRequest request) => request.SortOrder != "asc";

    /// <summary>Map một trang dòng từ repository sang <see cref="PagedResponse{T}"/>.</summary>
    protected static PagedResponse<TResponse> ToPaged<TRow, TResponse>(
        (IReadOnlyList<TRow> Items, int TotalCount) page, AdminQueryRequest request, Func<TRow, TResponse> map)
        => new()
        {
            Items = page.Items.Select(map).ToList(),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalItems = page.TotalCount
        };

    /// <summary>null = admin thấy mọi đơn; ngược lại chỉ đơn có chuyến do người này quản lý hoặc đơn thuê họ phụ trách.</summary>
    protected Guid? OrderScope => IsAdmin ? null : ActorId;
}
