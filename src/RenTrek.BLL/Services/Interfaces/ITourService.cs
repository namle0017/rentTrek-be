using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.DTOs.Tour;

namespace RenTrek.BLL.Services.Interfaces;

public interface ITourService
{
    Task<PagedResult<TourResponse>> GetToursAsync(TourQueryRequest query, CancellationToken cancellationToken = default);
    Task<TourDetailResponse> GetTourByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
