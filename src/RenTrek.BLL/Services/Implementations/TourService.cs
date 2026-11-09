using AutoMapper;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Tour.Requests;
using RenTrek.BLL.DTOs.Tour.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Constants;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ tour.
/// </summary>
public class TourService : ITourService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public TourService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResponse<AdminTourListItemResponse>> GetAdminToursAsync(
        AdminTourQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        // Lọc theo trạng thái hiển thị: Active gộp vào Published, Expired gộp vào Archived; giá trị lạ → rỗng
        IReadOnlyCollection<string>? statuses = string.IsNullOrWhiteSpace(query.Status)
            ? null
            : query.Status.Trim().ToLowerInvariant() switch
            {
                "published" => [TourStatuses.Published, TourStatuses.Active],
                "archived" => [TourStatuses.Archived, TourStatuses.Expired],
                "draft" => [TourStatuses.Draft],
                _ => []
            };

        var (items, total) = await _unitOfWork.Tours.GetAdminPagedAsync(new AdminTourFilter
        {
            Search = query.Search,
            CategoryId = query.CategoryId,
            LocationId = query.LocationId,
            Statuses = statuses,
            Now = DateTimeOffset.UtcNow,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        }, cancellationToken);

        return new PagedResponse<AdminTourListItemResponse>
        {
            Items = items.Select(tour => new AdminTourListItemResponse
            {
                TourId = tour.TourId,
                TourFamilyId = tour.TourFamilyId,
                Name = tour.Name,
                CategoryName = tour.CategoryName,
                MainLocationName = tour.MainLocationName,
                DurationDays = tour.DurationDays,
                DurationNights = tour.DurationNights,
                BasePrice = tour.BasePrice,
                Version = tour.Version,
                IsItineraryLocked = tour.IsItineraryLocked,
                Status = tour.Status == TourStatuses.Active ? "Published"
                    : tour.Status == TourStatuses.Expired ? "Archived" : tour.Status,
                PrimaryImageUrl = tour.PrimaryImageUrl,
                UpcomingTripsCount = tour.UpcomingTripsCount
            }).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalItems = total
        };
    }

    public async Task<PagedResponse<TourResponse>> GetToursAsync(TourQueryRequest query, CancellationToken cancellationToken = default)
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

        return new PagedResponse<TourResponse>
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
