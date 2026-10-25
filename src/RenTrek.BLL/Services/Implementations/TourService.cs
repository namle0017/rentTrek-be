using AutoMapper;
using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.DTOs.Tour;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

public class TourService : ITourService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public TourService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResult<TourResponse>> GetToursAsync(TourQueryRequest query, CancellationToken cancellationToken = default)
    {
        var filterParams = new TourFilterParams(
            Status: query.Status,
            Search: query.Search,
            CategoryId: query.CategoryId,
            LocationId: query.LocationId,
            DifficultyLevel: query.DifficultyLevel,
            DurationDays: query.DurationDays,
            MinPrice: query.MinPrice,
            MaxPrice: query.MaxPrice,
            SortBy: query.SortBy,
            IsDescending: query.IsDescending,
            PageNumber: query.PageNumber,
            PageSize: query.PageSize);

        var (items, totalCount) = await _unitOfWork.Tours.GetPagedToursAsync(filterParams, cancellationToken);

        var mappedItems = _mapper.Map<List<TourResponse>>(items);

        return new PagedResult<TourResponse>
        {
            Items = mappedItems,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalItems = totalCount
        };
    }

    public async Task<TourDetailResponse> GetTourByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tour = await _unitOfWork.Tours.GetTourDetailByIdAsync(id, cancellationToken);

        if (tour is null)
        {
            throw new NotFoundException($"Không tìm thấy tour với mã '{id}'.");
        }

        return _mapper.Map<TourDetailResponse>(tour);
    }
}
