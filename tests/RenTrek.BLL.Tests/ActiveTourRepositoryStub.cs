using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.BLL.Tests;

/// <summary>
/// Repo tour giả cho test booking: tour nào cũng Active nên TripBookingService.CreateAsync qua được bước
/// "tour còn nhận booking". Hàm khác không dùng trong các test này nên ném NotSupportedException.
/// </summary>
internal sealed class ActiveTourRepositoryStub : ITourRepository
{
    public Task<Tour?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        => Task.FromResult<Tour?>(new Tour { Id = (Guid)id, Status = "Active" });

    public Task AddAsync(Tour entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task AddRangeAsync(IEnumerable<Tour> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public void Update(Tour entity) => throw new NotSupportedException();
    public void Remove(Tour entity) => throw new NotSupportedException();
    public void RemoveRange(IEnumerable<Tour> entities) => throw new NotSupportedException();
    public Task<(IReadOnlyList<Tour> Items, int TotalCount)> GetPagedToursAsync(TourFilterParams filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Tour?> GetTourDetailByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<int> CountByIdsAsync(IReadOnlyCollection<Guid> tourIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> AnyByMainLocationAsync(Guid locationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Tour?> GetForEditAsync(Guid tourId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Tour?> GetSuggestionsAsync(Guid tourId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> HasBookingsAsync(Guid tourId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<TourVersionRow>> GetFamilyVersionsAsync(Guid tourFamilyId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(IReadOnlyList<AdminTourRow> Items, int TotalCount)> GetAdminPagedAsync(AdminTourFilter filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
