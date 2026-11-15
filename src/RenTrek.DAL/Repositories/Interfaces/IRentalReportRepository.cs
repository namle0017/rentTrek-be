using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRentalReportRepository : IGenericRepository<RentalReport>
{
    /// <summary>Danh sách report kèm đơn (+khách), mới nhất trước.</summary>
    Task<(IReadOnlyList<RentalReport> Items, int TotalItems)> SearchAsync(
        string? status,
        string? reportType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Report kèm đơn, khách, người tạo / duyệt (có tracking).</summary>
    Task<RentalReport?> GetDetailAsync(Guid rentalReportId, CancellationToken cancellationToken = default);

    /// <summary>Report đang mở (Submitted) cùng loại của đơn, có tracking.</summary>
    Task<RentalReport?> GetOpenAsync(Guid rentalOrderId, string reportType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalReport>> GetByOrderAsync(Guid rentalOrderId, CancellationToken cancellationToken = default);
}
