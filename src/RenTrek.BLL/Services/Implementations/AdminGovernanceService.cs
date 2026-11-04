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

/// <summary>Cấu hình hệ thống, ẩn / hiện đánh giá và nhật ký audit.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public sealed class AdminGovernanceService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminGovernanceService
{
    public async Task<List<AdminSettingResponse>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        RequireAdmin();
        return await _unitOfWork.SystemConfigurations.Query().Where(x => !x.IsDeleted).OrderBy(x => x.ConfigKey)
            .Select(x => new AdminSettingResponse(x.ConfigKey, x.ConfigValue, x.ValueType, x.Description, x.UpdatedAt)).ToListAsync(cancellationToken);
    }

    public async Task UpdateSettingAsync(string key, UpdateSettingRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); var reason = request.Reason.Trim(); var value = request.ConfigValue.Trim();
        await AtomicAsync("setting:" + key, async () =>
        {
            var setting = await _unitOfWork.SystemConfigurations.QueryTracking().SingleOrDefaultAsync(x => x.ConfigKey == key && !x.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy cấu hình");
            var type = setting.ValueType.ToLowerInvariant();
            if (type is "int" or "integer") Check(int.TryParse(value, out var number) && number >= 0, "configValue", "Giá trị phải là số nguyên không âm");
            if (type is "decimal" or "number") Check(decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number) && number >= 0, "configValue", "Giá trị phải là số không âm");
            if (type is "bool" or "boolean") Check(bool.TryParse(value, out _), "configValue", "Giá trị phải là true hoặc false");
            if (type == "json")
            {
                try { using var document = System.Text.Json.JsonDocument.Parse(value); }
                catch (System.Text.Json.JsonException) { Check(false, "configValue", "JSON không hợp lệ"); }
            }
            if (key is "orders.holdExpiresMinutes" or "reconciliation.deadlineHours" or "rental.cleaningBufferHours" or "charges.dailyLateFeeVnd")
                Check(decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number) && number >= 0 && (key == "rental.cleaningBufferHours" || number > 0), "configValue", "Giá trị cấu hình không hợp lệ");
            var before = setting.ConfigValue; setting.ConfigValue = value; setting.UpdatedByUserId = ActorId; setting.UpdatedAt = Now;
            await AuditAsync("Update", "SystemConfiguration", setting.ConfigurationId, new { key, before, after = value, reason }, cancellationToken);
        }, cancellationToken);
    }

    public Task<PagedResponse<AdminReviewResponse>> GetReviewsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Reviews.Query().Where(x => !x.IsDeleted && !x.Participant.IsDeleted);
        if (request.Rating.HasValue) query = query.Where(x => x.Rating == request.Rating);
        if (request.TourId.HasValue) query = query.Where(x => x.Participant.Trip.TourId == request.TourId);
        if (request.GuideUserId.HasValue) query = query.Where(x => x.Participant.TripGroup != null && x.Participant.TripGroup.GuideUserId == request.GuideUserId);
        if (request.Status != null) query = query.Where(x => x.IsHidden == (request.Status == "Hidden"));
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim().ToLower(); query = query.Where(x => x.Comment != null && x.Comment.ToLower().Contains(search)); }
        return PageAsync(query, request, x => new AdminReviewResponse(x.ReviewId, x.Rating, x.Comment, x.Participant.FullName,
            x.Participant.Trip.Tour.Name, x.Participant.Trip.StartAt, x.Participant.TripGroup == null ? null : x.Participant.TripGroup.GuideUser.FullName,
            x.IsHidden, x.HiddenReason, x.CreatedAt), cancellationToken, "CreatedAt", "Rating");
    }

    public async Task SetReviewVisibilityAsync(Guid id, bool hidden, string? reason, CancellationToken cancellationToken)
    {
        RequireAdmin(); if (hidden) reason = Required(reason, "reason", 500);
        await AtomicAsync("review:" + id, async () =>
        {
            var review = await _unitOfWork.Reviews.QueryTracking().SingleOrDefaultAsync(x => x.ReviewId == id && !x.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đánh giá");
            var before = new { review.IsHidden, review.HiddenReason };
            review.IsHidden = hidden; review.HiddenReason = hidden ? reason : null; review.UpdatedAt = Now;
            await AuditAsync(hidden ? "Hide" : "Unhide", "Review", id, new { before, after = new { review.IsHidden, review.HiddenReason } }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<PagedResponse<AdminAuditResponse>> GetAuditLogsAsync(AdminQueryRequest request, Guid? userId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var query = _unitOfWork.AuditLogs.Query();
        if (userId.HasValue) query = query.Where(x => x.ActorUserId == userId || (x.EntityType == "Account" && x.EntityId == userId));
        if (request.ActorUserId.HasValue) query = query.Where(x => x.ActorUserId == request.ActorUserId);
        if (request.EntityId.HasValue) query = query.Where(x => x.EntityId == request.EntityId);
        if (request.EntityType != null) query = query.Where(x => x.EntityType == request.EntityType);
        if (request.Action != null) query = query.Where(x => x.Action == request.Action);
        if (request.FromDate.HasValue) query = query.Where(x => x.CreatedAt >= request.FromDate);
        if (request.ToDate.HasValue) query = query.Where(x => x.CreatedAt <= request.ToDate);
        return await PageAsync(query, request, x => new AdminAuditResponse(x.AuditLogId, x.ActorUserId,
            x.ActorUser == null ? null : x.ActorUser.FullName, x.Action, x.EntityType, x.EntityId,
            AuditDetails(x.Details), x.CreatedAt), cancellationToken);
    }
}
