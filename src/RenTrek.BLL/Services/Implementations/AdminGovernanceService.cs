using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Models;
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
        var settings = await _unitOfWork.SystemConfigurations.GetActiveAsync(cancellationToken);
        return settings.Select(x => new AdminSettingResponse(x.ConfigKey, x.ConfigValue, x.ValueType, x.Description, x.UpdatedAt)).ToList();
    }

    public async Task UpdateSettingAsync(string key, UpdateSettingRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); var reason = request.Reason.Trim(); var value = request.ConfigValue.Trim();
        await AtomicAsync("setting:" + key, async () =>
        {
            var setting = await _unitOfWork.SystemConfigurations.GetForUpdateAsync(key, cancellationToken)
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

    public async Task<PagedResponse<AdminReviewResponse>> GetReviewsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        CheckPaging(request);
        var page = await _unitOfWork.Reviews.GetAdminPagedAsync(new AdminReviewFilter
        {
            Rating = request.Rating,
            TourId = request.TourId,
            GuideUserId = request.GuideUserId,
            IsHidden = request.Status == null ? null : request.Status == "Hidden",
            Search = request.Search,
            SortBy = request.SortBy,
            IsDescending = IsDescending(request),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        }, cancellationToken);
        return ToPaged(page, request, x => new AdminReviewResponse(x.ReviewId, x.Rating, x.Comment, x.CustomerFullName,
            x.TourName, x.TripStartAt, x.GuideFullName, x.IsHidden, x.HiddenReason, x.CreatedAt));
    }

    public async Task SetReviewVisibilityAsync(Guid id, bool hidden, string? reason, CancellationToken cancellationToken)
    {
        RequireAdmin(); if (hidden) reason = Required(reason, "reason", 500);
        await AtomicAsync("review:" + id, async () =>
        {
            var review = await _unitOfWork.Reviews.GetForUpdateAsync(id, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đánh giá");
            var before = new { review.IsHidden, review.HiddenReason };
            review.IsHidden = hidden; review.HiddenReason = hidden ? reason : null; review.UpdatedAt = Now;
            await AuditAsync(hidden ? "Hide" : "Unhide", "Review", id, new { before, after = new { review.IsHidden, review.HiddenReason } }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<PagedResponse<AdminAuditResponse>> GetAuditLogsAsync(AdminQueryRequest request, Guid? userId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        CheckPaging(request);
        var page = await _unitOfWork.AuditLogs.GetPagedAsync(new AuditLogFilter
        {
            RelatedUserId = userId,
            ActorUserId = request.ActorUserId,
            EntityId = request.EntityId,
            EntityType = request.EntityType,
            Action = request.Action,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            SortBy = request.SortBy,
            IsDescending = IsDescending(request),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        }, cancellationToken);
        return ToPaged(page, request, ToAuditResponse);
    }
}
