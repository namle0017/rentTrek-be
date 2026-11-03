using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IAssetAssignmentRepository : IGenericRepository<AssetAssignment>
{
    Task<AssetAssignment?> GetAssignmentWithAssetAndTripItemAsync(Guid assignmentId, CancellationToken cancellationToken = default);
    Task<List<AssetAssignment>> GetAssignmentsByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<List<AssetAssignment>> GetAssignmentsByAssetCodesAndTripIdAsync(Guid tripId, IEnumerable<string> assetCodes, CancellationToken cancellationToken = default);
}
