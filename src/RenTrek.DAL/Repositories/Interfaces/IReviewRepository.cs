using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IReviewRepository : IGenericRepository<Review>
{
    /// <summary>Đánh giá chưa xoá (participant chưa xoá) cho trang quản trị.</summary>
    Task<(IReadOnlyList<AdminReviewRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminReviewFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Đánh giá chưa xoá theo id (tracking để cập nhật).</summary>
    Task<Review?> GetForUpdateAsync(Guid reviewId, CancellationToken cancellationToken = default);

    /// <summary>Lấy thông tin sở hữu, điều kiện chuyến/đơn và việc đã tồn tại review hay chưa cho người tham gia.</summary>
    Task<ParticipantReviewContextRow?> GetParticipantReviewContextAsync(
        Guid participantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy đánh giá chưa xóa theo participantId (tracking để chỉnh sửa).</summary>
    Task<Review?> GetByParticipantIdForUpdateAsync(
        Guid participantId,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy TourFamilyId của tour công khai (chưa xóa và khác Draft); trả null nếu không tồn tại hoặc đang Draft.</summary>
    Task<Guid?> GetPublicTourFamilyIdAsync(Guid tourId, CancellationToken cancellationToken = default);

    /// <summary>Lấy phân bố sao và danh sách phân trang đánh giá công khai theo họ tour, mới nhất trước.</summary>
    Task<TourReviewsSummaryRow> GetTourFamilyReviewsAsync(
        Guid tourFamilyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
