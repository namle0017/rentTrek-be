using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Tour.Requests;
using RenTrek.BLL.DTOs.Tour.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface ITourService
{
    Task<PagedResponse<TourResponse>> GetToursAsync(TourQueryRequest query, CancellationToken cancellationToken = default);
    Task<TourDetailResponse> GetTourByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
