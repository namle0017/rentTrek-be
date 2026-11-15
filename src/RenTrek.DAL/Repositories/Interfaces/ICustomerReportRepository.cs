using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>
/// Truy cập báo cáo khách gửi về đơn thuê / chuyến đi.
/// </summary>
public interface ICustomerReportRepository
{
    Task AddAsync(CustomerReport report, CancellationToken cancellationToken = default);

    /// <summary>Báo cáo kèm khách, người xử lý, đơn (+ chuyến / tour nếu là booking); có tracking.</summary>
    Task<CustomerReport?> GetDetailAsync(Guid customerReportId, CancellationToken cancellationToken = default);

    /// <summary>Danh sách cho admin, mới nhất trước; keyword khớp mã đơn, tên / email / SĐT khách.</summary>
    Task<(IReadOnlyList<CustomerReport> Items, int TotalItems)> SearchAsync(
        string? sourceType,
        string? status,
        string? keyword,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Số báo cáo theo trạng thái (cho badge / thẻ tổng quan).</summary>
    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Báo cáo của chính khách, lọc theo đơn nếu có.</summary>
    Task<IReadOnlyList<CustomerReport>> GetByReporterAsync(Guid reporterUserId, Guid? orderId, CancellationToken cancellationToken = default);

    /// <summary>Đơn kèm booking chuyến (chuyến, tour, người tham gia) để kiểm tra quyền gửi báo cáo; không tracking.</summary>
    Task<Order?> GetOrderForReportAsync(Guid orderId, CancellationToken cancellationToken = default);
}
