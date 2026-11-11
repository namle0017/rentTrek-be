using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Requests;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Review.Requests;
using RenTrek.BLL.DTOs.Review.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực thi nghiệp vụ gửi, cập nhật đánh giá sau chuyến đi và truy vấn đánh giá công khai theo họ tour (FR-V04, FR-V05, FR-V06).
/// </summary>
public sealed class ReviewService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IReviewService
{
    /// <summary>
    /// Hành khách liên kết tài khoản gửi đánh giá 1–5 sao duy nhất cho chuyến đi đã hoàn thành và thanh toán hợp lệ.
    /// </summary>
    public async Task<ParticipantReviewResponse> CreateReviewAsync(
        Guid participantId,
        SaveReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequiredUserId;
        var context = await unitOfWork.Reviews.GetParticipantReviewContextAsync(participantId, userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy thông tin tham gia chuyến đi.", ErrorCodes.NotFound);

        EnsureReviewEligible(context);

        if (context.HasReview)
            throw new ConflictException("Bạn đã đánh giá lượt tham gia chuyến đi này rồi.", ErrorCodes.ReviewAlreadyExists);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var review = new Review
        {
            ReviewId = Guid.NewGuid(),
            ParticipantId = participantId,
            Rating = request.Rating,
            Comment = NormalizeComment(request.Comment),
            IsHidden = false,
            HiddenReason = null,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await unitOfWork.Reviews.AddAsync(review, cancellationToken);
        if (!await unitOfWork.TrySaveChangesIgnoringUniqueViolationAsync(cancellationToken))
            throw new ConflictException("Bạn đã đánh giá lượt tham gia chuyến đi này rồi.", ErrorCodes.ReviewAlreadyExists);

        return MapParticipantReview(review);
    }

    /// <summary>
    /// Chỉnh sửa đánh giá của chính người tham gia trong khoảng thời gian cấu hình kể từ lúc tạo; giữ nguyên cờ ẩn nếu bị kiểm duyệt.
    /// </summary>
    public async Task<ParticipantReviewResponse> UpdateReviewAsync(
        Guid participantId,
        SaveReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequiredUserId;
        var context = await unitOfWork.Reviews.GetParticipantReviewContextAsync(participantId, userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy thông tin tham gia chuyến đi.", ErrorCodes.NotFound);

        var review = await unitOfWork.Reviews.GetByParticipantIdForUpdateAsync(context.ParticipantId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đánh giá cho lượt tham gia này.", ErrorCodes.NotFound);

        var windowDays = await GetEditWindowDaysAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (now > review.CreatedAt.AddDays(windowDays))
        {
            throw new ConflictException(
                $"Đã quá thời hạn {windowDays} ngày để chỉnh sửa đánh giá.",
                ErrorCodes.ReviewEditWindowClosed);
        }

        review.Rating = request.Rating;
        review.Comment = NormalizeComment(request.Comment);
        review.UpdatedAt = now;

        unitOfWork.Reviews.Update(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapParticipantReview(review);
    }

    /// <summary>
    /// Tổng hợp điểm trung bình, phân bố 1–5 sao và danh sách đánh giá công khai theo họ tour (loại trừ đánh giá bị ẩn hoặc đã xóa).
    /// </summary>
    public async Task<TourReviewsResponse> GetTourReviewsAsync(
        Guid tourId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1
            ? PagingRequest.DefaultPageSize
            : Math.Min(pageSize, PagingRequest.MaxPageSize);

        var tourFamilyId = await unitOfWork.Reviews.GetPublicTourFamilyIdAsync(tourId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy tour với mã '{tourId}'.", ErrorCodes.NotFound);

        var summary = await unitOfWork.Reviews.GetTourFamilyReviewsAsync(
            tourFamilyId,
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        var distribution = new Dictionary<int, int>(5);
        long weightedSum = 0;
        for (var star = 1; star <= 5; star++)
        {
            var count = summary.RatingCounts.GetValueOrDefault(star);
            distribution[star] = count;
            weightedSum += (long)star * count;
        }

        var averageRating = summary.TotalCount == 0
            ? 0m
            : Math.Round(weightedSum / (decimal)summary.TotalCount, 1, MidpointRounding.AwayFromZero);

        var pagedItems = new PagedResponse<TourReviewItemResponse>
        {
            Items = summary.Items.Select(x => new TourReviewItemResponse
            {
                ReviewId = x.ReviewId,
                Rating = x.Rating,
                Comment = x.Comment,
                CreatedAt = x.CreatedAt,
                ReviewerName = x.ReviewerName,
                TripStartAt = x.TripStartAt
            }).ToList(),
            PageNumber = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = summary.TotalCount
        };

        return new TourReviewsResponse
        {
            AverageRating = averageRating,
            ReviewCount = summary.TotalCount,
            Distribution = distribution,
            Reviews = pagedItems
        };
    }

    private async Task<int> GetEditWindowDaysAsync(CancellationToken cancellationToken)
    {
        var raw = await unitOfWork.SystemConfigurations.GetValueAsync(
            ReviewConfigKeys.ReviewEditWindowDays,
            cancellationToken);

        return int.TryParse(raw, out var days) && days >= 0
            ? days
            : ReviewConfigKeys.DefaultReviewEditWindowDays;
    }

    private static void EnsureReviewEligible(ParticipantReviewContextRow context)
    {
        if (string.Equals(context.ParticipantStatus, BookingParticipantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Suất tham gia đã bị hủy nên không thể đánh giá.", ErrorCodes.ReviewNotEligible);

        if (string.Equals(context.OrderStatus, OrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Đơn đặt chỗ đã bị hủy nên không thể đánh giá.", ErrorCodes.ReviewNotEligible);

        if (!string.Equals(context.TripStatus, TripStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Chuyến đi chưa hoàn thành nên chưa thể đánh giá.", ErrorCodes.ReviewNotEligible);

        var isPaidEligible =
            string.Equals(context.OrderPaymentStatus, OrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.OrderPaymentStatus, OrderPaymentStatuses.PartiallyRefunded, StringComparison.OrdinalIgnoreCase);

        if (!isPaidEligible)
            throw new ConflictException("Đơn hàng chưa thanh toán hợp lệ nên chưa thể đánh giá.", ErrorCodes.ReviewNotEligible);
    }

    private static string? NormalizeComment(string? comment)
        => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

    private static ParticipantReviewResponse MapParticipantReview(Review review)
        => new()
        {
            ReviewId = review.ReviewId,
            ParticipantId = review.ParticipantId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };
}
