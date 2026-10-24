using AutoMapper;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.DTOs.Tour;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
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
        var toursQuery = _unitOfWork.Repository<Tour>()
            .Query()
            .Include(t => t.Category)
            .Include(t => t.MainLocation)
            .Include(t => t.Images)
            .Include(t => t.Trips)
            .AsQueryable();

        // Lọc theo Tab trạng thái: "Active" (Đang mở) hoặc "Expired" (Hết hạn)
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLowerInvariant();
            if (status is "active" or "dang mo" or "đang mở")
            {
                toursQuery = toursQuery.Where(t => t.Status == "Active");
            }
            else if (status is "expired" or "het han" or "hết hạn")
            {
                toursQuery = toursQuery.Where(t => t.Status == "Expired");
            }
            else
            {
                toursQuery = toursQuery.Where(t => t.Status.ToLower() == status);
            }
        }

        // Tìm kiếm theo từ khóa
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            toursQuery = toursQuery.Where(t =>
                t.Name.ToLower().Contains(search) ||
                t.MainLocation.Name.ToLower().Contains(search) ||
                (t.Description != null && t.Description.ToLower().Contains(search)));
        }

        // Lọc theo Category
        if (query.CategoryId.HasValue)
        {
            toursQuery = toursQuery.Where(t => t.CategoryId == query.CategoryId.Value);
        }

        // Lọc theo Location
        if (query.LocationId.HasValue)
        {
            toursQuery = toursQuery.Where(t => t.MainLocationId == query.LocationId.Value);
        }

        // Lọc theo Độ khó (Dễ, Trung bình, Thử thách, Khó)
        if (!string.IsNullOrWhiteSpace(query.DifficultyLevel))
        {
            var difficulty = query.DifficultyLevel.Trim().ToLower();
            toursQuery = toursQuery.Where(t => t.DifficultyLevel.ToLower() == difficulty);
        }

        // Lọc theo Số ngày
        if (query.DurationDays.HasValue)
        {
            toursQuery = toursQuery.Where(t => t.DurationDays == query.DurationDays.Value);
        }

        // Lọc theo Khoảng giá
        if (query.MinPrice.HasValue)
        {
            toursQuery = toursQuery.Where(t => t.BasePrice >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            toursQuery = toursQuery.Where(t => t.BasePrice <= query.MaxPrice.Value);
        }

        // Sắp xếp
        toursQuery = query.SortBy.ToLowerInvariant() switch
        {
            "price" => query.IsDescending ? toursQuery.OrderByDescending(t => t.BasePrice) : toursQuery.OrderBy(t => t.BasePrice),
            "duration" => query.IsDescending ? toursQuery.OrderByDescending(t => t.DurationDays) : toursQuery.OrderBy(t => t.DurationDays),
            "name" => query.IsDescending ? toursQuery.OrderByDescending(t => t.Name) : toursQuery.OrderBy(t => t.Name),
            _ => query.IsDescending ? toursQuery.OrderByDescending(t => t.CreatedAt) : toursQuery.OrderBy(t => t.CreatedAt)
        };

        var totalItems = await toursQuery.CountAsync(cancellationToken);

        var pagedEntities = await toursQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var mappedItems = _mapper.Map<List<TourResponse>>(pagedEntities);

        return new PagedResult<TourResponse>
        {
            Items = mappedItems,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<TourDetailResponse> GetTourByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tour = await _unitOfWork.Repository<Tour>()
            .Query()
            .Include(t => t.Category)
            .Include(t => t.MainLocation)
            .Include(t => t.Images)
            .Include(t => t.Activities)
            .Include(t => t.ProvidedItems)
            .Include(t => t.PreparationItems)
            .Include(t => t.Trips)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tour is null)
        {
            throw new NotFoundException($"Không tìm thấy tour với mã '{id}'.");
        }

        return _mapper.Map<TourDetailResponse>(tour);
    }
}
