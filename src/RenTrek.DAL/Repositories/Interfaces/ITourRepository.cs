using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ITourRepository : IGenericRepository<Tour>
{
    Task<(IReadOnlyList<Tour> Items, int TotalCount)> GetPagedToursAsync(
        TourFilterParams filter,
        CancellationToken cancellationToken = default);

    Task<Tour?> GetTourDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
