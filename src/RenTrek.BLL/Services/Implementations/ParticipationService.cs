using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Participation.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Xử lý nghiệp vụ theo dõi chuyến đi đã liên kết tài khoản và trạng thái đọc bài viết của người tham gia (FR-N04, FR-N05).
/// </summary>
public sealed class ParticipationService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IParticipationService
{
    /// <summary>
    /// Lấy danh sách lượt tham gia của người dùng hiện tại kèm số bài viết chưa đọc và trạng thái đủ điều kiện đánh giá.
    /// </summary>
    public async Task<IReadOnlyList<MyParticipationResponse>> GetMyParticipationsAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequiredUserId;
        var rows = await unitOfWork.ParticipantPostReads.GetMyParticipationsAsync(userId, cancellationToken);
        return rows.Select(MapParticipation).ToList();
    }

    /// <summary>
    /// Trả về danh sách bài viết Published của tour gắn với chuyến đi, chỉ khi người gọi là chủ participant hợp lệ.
    /// </summary>
    public async Task<IReadOnlyList<ParticipantPostResponse>> GetParticipantPostsAsync(
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequiredUserId;
        var owned = await unitOfWork.ParticipantPostReads.IsOwnedActiveParticipantAsync(participantId, userId, cancellationToken);
        if (!owned)
            throw new NotFoundException("Không tìm thấy thông tin tham gia chuyến đi.", ErrorCodes.NotFound);

        var rows = await unitOfWork.ParticipantPostReads.GetParticipantPostsAsync(participantId, cancellationToken);
        return rows.Select(MapPost).ToList();
    }

    /// <summary>
    /// Tạo hoặc trả lại bản ghi đã đọc bài viết (idempotent); xử lý an toàn khi trùng khóa duy nhất đồng thời.
    /// </summary>
    public async Task<ParticipantPostReadResponse> MarkPostAsReadAsync(
        Guid participantId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequiredUserId;
        var accessible = await unitOfWork.ParticipantPostReads.CanAccessTourPostAsync(participantId, userId, postId, cancellationToken);
        if (!accessible)
            throw new NotFoundException("Không tìm thấy bài viết hoặc thông tin tham gia chuyến đi.", ErrorCodes.NotFound);

        var existing = await unitOfWork.ParticipantPostReads.GetByParticipantAndPostAsync(participantId, postId, cancellationToken);
        if (existing is not null)
            return MapReadResponse(existing);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = new ParticipantPostRead
        {
            ParticipantPostReadId = Guid.NewGuid(),
            ParticipantId = participantId,
            PostId = postId,
            ReadAt = now,
            CreatedAt = now
        };

        await unitOfWork.ParticipantPostReads.AddAsync(entity, cancellationToken);
        if (await unitOfWork.TrySaveChangesIgnoringUniqueViolationAsync(cancellationToken))
            return MapReadResponse(entity);

        // Request đồng thời khác đã chèn thành công nên đọc lại để trả đúng ReadAt ban đầu
        var concurrentExisting = await unitOfWork.ParticipantPostReads.GetByParticipantAndPostAsync(participantId, postId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bản ghi đọc bài viết.", ErrorCodes.NotFound);

        return MapReadResponse(concurrentExisting);
    }

    private static MyParticipationResponse MapParticipation(MyParticipationRow row)
        => new()
        {
            ParticipantId = row.ParticipantId,
            TripId = row.TripId,
            TourId = row.TourId,
            TourName = row.TourName,
            StartAt = row.StartAt,
            EndAt = row.EndAt,
            TripStatus = row.TripStatus,
            UnreadPostCount = row.UnreadPostCount,
            HasReview = row.HasReview,
            CanReview = IsReviewEligible(row)
        };

    private static bool IsReviewEligible(MyParticipationRow row)
        => !row.HasReview
            && string.Equals(row.TripStatus, TripStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(row.OrderPaymentStatus, OrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(row.OrderPaymentStatus, OrderPaymentStatuses.PartiallyRefunded, StringComparison.OrdinalIgnoreCase))
            && !string.Equals(row.OrderStatus, OrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(row.ParticipantStatus, BookingParticipantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);

    private static ParticipantPostResponse MapPost(ParticipantPostRow row)
        => new()
        {
            PostId = row.PostId,
            Title = row.Title,
            Slug = row.Slug,
            IsRead = row.ReadAt.HasValue,
            ReadAt = row.ReadAt
        };

    private static ParticipantPostReadResponse MapReadResponse(ParticipantPostRead entity)
        => new()
        {
            ParticipantPostReadId = entity.ParticipantPostReadId,
            ParticipantId = entity.ParticipantId,
            PostId = entity.PostId,
            IsRead = true,
            ReadAt = entity.ReadAt
        };
}
