using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Internal;

/// <summary>
/// Quyết toán cọc sau khi trả đồ (admin duyệt): phí hợp lệ trừ vào cọc; còn dư thì mở hồ sơ hoàn chờ khách nhập STK,
/// thiếu thì tạo khoản thu thêm + report ExtraPayment và khoá quyền thuê mới tới khi khách trả.
/// </summary>
public interface IRentalSettlementService
{
    /// <summary>Chạy trong transaction của caller với đồ thị đơn đã tải có tracking; không SaveChanges.</summary>
    Task<RentalSettlement> SettleAsync(Order order, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Lý do chưa quyết toán được; null nếu đủ điều kiện.</summary>
    string? GetBlockedReason(Order order);
}
