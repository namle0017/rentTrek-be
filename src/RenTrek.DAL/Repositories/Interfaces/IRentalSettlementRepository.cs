using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRentalSettlementRepository : IGenericRepository<RentalSettlement>
{
    Task<RentalSettlement?> GetByOrderAsync(Guid rentalOrderId, CancellationToken cancellationToken = default);

    /// <summary>Quyết toán có khoản thu thêm trỏ tới payment này (có tracking).</summary>
    Task<RentalSettlement?> GetByExtraPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);
}
