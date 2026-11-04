using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRentalChargeRepository : IGenericRepository<RentalCharge>
{
    Task<List<RentalCharge>> GetChargesByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default);
    Task<RentalCharge?> GetChargeByIdWithDetailsAsync(Guid chargeId, CancellationToken cancellationToken = default);
}
