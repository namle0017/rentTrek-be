using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ITripRepository : IGenericRepository<Trip>
{
    Task<Trip?> GetTripWithTourAndRequirementsAsync(Guid tripId, CancellationToken cancellationToken = default);
}
