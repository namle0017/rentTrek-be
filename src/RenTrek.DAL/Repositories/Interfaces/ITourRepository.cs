using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public record TourFilterParams(
    string? Status = null,
    string? Search = null,
    Guid? CategoryId = null,
    Guid? LocationId = null,
    string? DifficultyLevel = null,
    int? DurationDays = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string SortBy = "createdAt",
    bool IsDescending = true,
    int PageNumber = 1,
    int PageSize = 10);

public interface ITourRepository : IGenericRepository<Tour>
{
    Task<(IReadOnlyList<Tour> Items, int TotalCount)> GetPagedToursAsync(
        TourFilterParams filter,
        CancellationToken cancellationToken = default);

    Task<Tour?> GetTourDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
