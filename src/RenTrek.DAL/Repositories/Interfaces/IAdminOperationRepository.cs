using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>
/// Khoá thao tác quản trị và các truy vấn dashboard / báo cáo.
/// <c>scopeUserId</c> null = admin (không giới hạn); có giá trị = chỉ dữ liệu staff đó phụ trách.
/// </summary>
public interface IAdminOperationRepository
{
    Task AcquireLockAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Bộ đếm dashboard; số liệu hoàn tiền chỉ tính khi scopeUserId null.</summary>
    Task<AdminDashboardStatsRow> GetDashboardStatsAsync(Guid? scopeUserId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Đồ cấp còn thiếu của chuyến khởi hành trong 7 ngày, sớm nhất trước.</summary>
    Task<List<MissingGearRow>> GetMissingGearAsync(Guid? scopeUserId, DateTimeOffset now, int take, CancellationToken cancellationToken = default);

    /// <summary>Đơn thuê quá hạn trả còn tài sản đang Issued, quá hạn lâu nhất trước.</summary>
    Task<List<OrderRefRow>> GetOverdueRentalOrdersAsync(Guid? scopeUserId, DateTime now, int take, CancellationToken cancellationToken = default);

    /// <summary>Đơn PendingPayment hết hạn giữ chỗ trong (now, until], sắp hết hạn nhất trước.</summary>
    Task<List<OrderRefRow>> GetExpiringOrdersAsync(Guid? scopeUserId, DateTimeOffset now, DateTimeOffset until, int take, CancellationToken cancellationToken = default);

    /// <summary>Đơn có hoàn tiền thất bại, mới nhất trước.</summary>
    Task<List<OrderRefRow>> GetFailedRefundOrdersAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>Tổng thanh toán Succeeded có ProcessedAt trong [from, to].</summary>
    Task<PaymentTotalsRow> GetPaymentTotalsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Số đơn đặt chuyến tạo trong [from, to] và số tài sản đang phát / sẵn sàng.</summary>
    Task<AdminKpiCountsRow> GetKpiCountsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Thanh toán Succeeded có ProcessedAt trong [from, to].</summary>
    Task<List<RevenuePaymentRow>> GetSucceededPaymentsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Lấp đầy của chuyến chưa huỷ khởi hành trong [from, to], lọc tour nếu có.</summary>
    Task<List<TripOccupancyRow>> GetOccupancyAsync(DateTimeOffset from, DateTimeOffset to, Guid? tourId, CancellationToken cancellationToken = default);

    /// <summary>Tài sản chưa xoá (lọc danh mục nếu có) kèm các lần phát giao với [from, to).</summary>
    Task<List<AssetUsageRow>> GetAssetUsageAsync(DateTime from, DateTime to, Guid? categoryId, CancellationToken cancellationToken = default);
}
