using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IOrderRepository : IGenericRepository<Order>
{
    /// <summary>
    /// Danh sách đơn của khách (đơn thuê và đơn đặt chuyến) đã lọc / sắp xếp / phân trang, chiếu thẳng trong SQL
    /// (tên dòng đầu tiên, số dòng, tên tour) để tránh N+1.
    /// </summary>
    Task<(IReadOnlyList<MyOrderRow> Items, int TotalCount)> GetMyOrdersPagedAsync(
        Guid customerUserId,
        MyOrderFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách đơn trang quản trị (lọc theo phạm vi người xem).</summary>
    Task<(IReadOnlyList<AdminOrderRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminOrderFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Đầu trang chi tiết đơn quản trị theo mã đơn, trong phạm vi <paramref name="scopeUserId"/> (null = mọi đơn).</summary>
    Task<AdminOrderRow?> GetAdminRowAsync(string orderCode, Guid? scopeUserId, CancellationToken cancellationToken = default);

    /// <summary>Mã đơn theo id (chỉ đọc), trong phạm vi <paramref name="scopeUserId"/>; null nếu không có.</summary>
    Task<string?> GetOrderCodeAsync(Guid orderId, Guid? scopeUserId, CancellationToken cancellationToken = default);

    /// <summary>Đơn (tracking, kèm Payments) theo mã đơn, trong phạm vi <paramref name="scopeUserId"/>.</summary>
    Task<Order?> GetAdminForUpdateAsync(string orderCode, Guid? scopeUserId, CancellationToken cancellationToken = default);

    /// <summary>Hành khách chưa xoá của đơn (tracking).</summary>
    Task<List<BookingParticipant>> GetParticipantsAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Một hành khách chưa xoá của đơn (tracking, kèm Trip).</summary>
    Task<BookingParticipant?> GetParticipantForUpdateAsync(Guid orderId, Guid participantId, CancellationToken cancellationToken = default);

    /// <summary>Thanh toán của đơn, mới nhất trước (chỉ đọc).</summary>
    Task<List<Payment>> GetPaymentsAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Khoản thu thủ công đã ghi với mã tham chiếu này (chỉ đọc).</summary>
    Task<Payment?> GetManualCollectionAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Dòng thuê chưa xoá (chỉ đọc) kèm combo / biến thể, snapshot thành phần và phân bổ tài sản.</summary>
    Task<List<RentalOrderItem>> GetRentalLinesAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Đơn thuê (chỉ đọc) kèm người phụ trách.</summary>
    Task<RentalOrder?> GetRentalWithHandlerAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Khuyến mãi áp dụng cho đơn (tracking).</summary>
    Task<List<AppliedPromotion>> GetAppliedPromotionsAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Phân bổ tài sản của dòng thuê hoặc quà tặng thuộc đơn (tracking).</summary>
    Task<List<AssetAssignment>> GetAssignmentsForUpdateAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Lời mời hành khách chưa thu hồi / chưa dùng của đơn (tracking).</summary>
    Task<List<ParticipantInvitation>> GetOpenInvitationsForUpdateAsync(Guid orderId, CancellationToken cancellationToken = default);
}
