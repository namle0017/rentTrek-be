using AutoMapper;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.DTOs.ProductBundle;
using RenTrek.BLL.DTOs.Rental;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ product bundle (combo thiết bị): danh sách, chi tiết và khả dụng theo khoảng thuê.
/// </summary>
public class ProductBundleService : IProductBundleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly TimeProvider _timeProvider;

    public ProductBundleService(IUnitOfWork unitOfWork, IMapper mapper, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _timeProvider = timeProvider;
    }

    public async Task<PagedResult<ProductBundleResponse>> GetAllAsync(ProductBundleQueryRequest request, CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(request.From, request.To);
        var filter = new ProductBundleFilter
        {
            Search = request.Search?.Trim(),
            CategoryId = request.CategoryId,
            SortBy = request.SortBy,
            IsDescending = request.IsDescending,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        Dictionary<Guid, int>? availabilityByBundle = null;
        if (period is not null && request.OnlyAvailable)
        {
            // Lọc "chỉ còn hàng" phải tính tồn cho mọi combo khớp trước khi phân trang (một truy vấn tồn cho tất cả)
            var candidates = await _unitOfWork.ProductBundles.GetActiveComponentsAsync(filter, cancellationToken);
            availabilityByBundle = await ComputeBundleAvailabilityAsync(candidates, period, cancellationToken);
            filter.RestrictToBundleIds = availabilityByBundle.Where(item => item.Value > 0).Select(item => item.Key).ToList();
        }

        var (rows, totalCount) = await _unitOfWork.ProductBundles.GetPagedActiveAsync(filter, cancellationToken);

        if (period is not null && availabilityByBundle is null)
        {
            availabilityByBundle = await ComputeBundleAvailabilityAsync(
                rows.SelectMany(row => row.Components).ToList(), period, cancellationToken);
        }

        var items = rows.Select(row =>
        {
            var response = _mapper.Map<ProductBundleResponse>(row);
            if (period is not null)
                response.Availability = ToAvailability(period, availabilityByBundle!.GetValueOrDefault(row.Id), null);
            return response;
        }).ToList();

        return new PagedResult<ProductBundleResponse>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalItems = totalCount
        };
    }

    public async Task<ProductBundleDetailResponse> GetByIdAsync(Guid bundleId, RentalPeriodQueryRequest request, CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(request.From, request.To);

        var detail = await _unitOfWork.ProductBundles.GetActiveDetailAsync(bundleId, cancellationToken)
            ?? throw new NotFoundException("Combo không tồn tại hoặc đã ngừng kinh doanh", ErrorCodes.BundleNotFound);

        var response = _mapper.Map<ProductBundleDetailResponse>(detail);
        if (period is null)
            return response;

        var variantIds = detail.Components.Select(component => component.VariantId).Distinct().ToList();
        var availableByVariant = await GetAvailableByVariantAsync(variantIds, period, cancellationToken);

        foreach (var component in response.Components)
            component.AvailableQuantity = availableByVariant.GetValueOrDefault(component.ProductVariantId);

        var (quantity, limitingVariantId) = BundleAvailabilityOf(detail.Components, availableByVariant);
        var limitingComponent = response.Components
            .FirstOrDefault(component => component.ProductVariantId == limitingVariantId)?.ProductName;
        response.Availability = ToAvailability(period, quantity, limitingComponent);

        return response;
    }

    /// <summary>Null khi không truyền from/to; chỉ truyền một trong hai thì 400; còn lại kiểm tra theo RentalPeriodCalculator.</summary>
    private RentalPeriod? ResolvePeriod(DateTime? from, DateTime? to)
    {
        if (!from.HasValue && !to.HasValue)
            return null;

        if (!from.HasValue || !to.HasValue)
            throw new BadRequestException("Cần truyền đủ cả from và to", ErrorCodes.InvalidRentalPeriod);

        return RentalPeriodCalculator.Validate(from.Value, to.Value, _timeProvider.GetUtcNow().UtcDateTime);
    }

    private async Task<Dictionary<Guid, int>> GetAvailableByVariantAsync(
        IReadOnlyCollection<Guid> variantIds, RentalPeriod period, CancellationToken cancellationToken)
    {
        if (variantIds.Count == 0)
            return [];

        var filter = await RentalAvailabilityFilterFactory.CreateAsync(
            _unitOfWork, period, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return await _unitOfWork.EquipmentAssets.GetAvailableCountsAsync(variantIds, filter, cancellationToken);
    }

    /// <summary>Số combo khả dụng theo từng combo, dùng một truy vấn tồn cho mọi biến thể thành phần.</summary>
    private async Task<Dictionary<Guid, int>> ComputeBundleAvailabilityAsync(
        IReadOnlyList<ProductBundleComponentRow> components, RentalPeriod period, CancellationToken cancellationToken)
    {
        var availableByVariant = await GetAvailableByVariantAsync(
            components.Select(component => component.VariantId).Distinct().ToList(), period, cancellationToken);

        return components
            .GroupBy(component => component.BundleId)
            .ToDictionary(group => group.Key, group => BundleAvailabilityOf(group.ToList(), availableByVariant).Quantity);
    }

    /// <summary>Combo có thành phần ngừng kinh doanh hoặc không có thành phần thì không thuê được (0).</summary>
    private static (int Quantity, Guid? LimitingVariantId) BundleAvailabilityOf(
        IReadOnlyList<ProductBundleComponentRow> components, IReadOnlyDictionary<Guid, int> availableByVariant)
    {
        var inactive = components.FirstOrDefault(component => !component.IsActive);
        if (inactive is not null)
            return (0, inactive.VariantId);

        return RentalAvailabilityCalculator.BundleAvailability(
            components.Select(component => new RentalLineComponent
            {
                ProductVariantId = component.VariantId,
                VariantName = component.VariantName,
                QuantityPerUnit = component.Quantity
            }),
            availableByVariant);
    }

    private static BundleAvailabilityResponse ToAvailability(RentalPeriod period, int availableQuantity, string? limitingComponent) => new()
    {
        From = period.StartUtc,
        To = period.EndUtc,
        AvailableQuantity = availableQuantity,
        IsAvailable = availableQuantity > 0,
        LimitingComponent = limitingComponent
    };
}
