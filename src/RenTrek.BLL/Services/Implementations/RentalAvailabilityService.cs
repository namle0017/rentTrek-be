using System.Globalization;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Rental.Internal;
using RenTrek.BLL.DTOs.RentalAvailability.Requests;
using RenTrek.BLL.DTOs.RentalAvailability.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ khả dụng thiết bị cho giỏ thuê: nạp dòng, tính tồn theo nhu cầu cộng dồn, chọn tài sản.
/// </summary>
public class RentalAvailabilityService : IRentalAvailabilityService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RentalAvailabilityService(IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<CheckRentalAvailabilityResponse> CheckAsync(CheckRentalAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        var period = RentalPeriodCalculator.Validate(request.RentalStartAt, request.RentalEndAt, UtcNow);
        var inputs = (request.Items ?? [])
            .Select(item => new RentalLineInput
            {
                ProductVariantId = item.ProductVariantId,
                BundleId = item.BundleId,
                Quantity = item.Quantity
            })
            .ToList();

        var lines = await ResolveLinesAsync(inputs, cancellationToken);
        var results = await EvaluateAsync(period, lines, cancellationToken);

        return new CheckRentalAvailabilityResponse
        {
            BillableDays = period.BillableDays,
            IsAllAvailable = results.All(result => result.IsAvailable),
            Items = results.Select(result =>
            {
                var message = BuildLineMessage(result, period);
                return new CheckRentalAvailabilityItemResponse
                {
                    Index = result.LineIndex,
                    IsAvailable = result.IsAvailable,
                    AvailableQuantity = result.AvailableQuantity,
                    Message = string.IsNullOrEmpty(message) ? null : message
                };
            }).ToList()
        };
    }

    public async Task<List<ResolvedRentalLineResult>> ResolveLinesAsync(IReadOnlyList<RentalLineInput> lines, CancellationToken cancellationToken = default)
    {
        if (lines is null || lines.Count == 0)
            throw new BadRequestException("Giỏ thuê phải có ít nhất một dòng", ErrorCodes.InvalidRentalItem);

        for (var index = 0; index < lines.Count; index++)
            ValidateShape(lines[index], index);

        var variantIds = lines.Where(line => line.ProductVariantId.HasValue)
            .Select(line => line.ProductVariantId!.Value).Distinct().ToList();
        var bundleIds = lines.Where(line => line.BundleId.HasValue)
            .Select(line => line.BundleId!.Value).Distinct().ToList();

        var variants = (await _unitOfWork.ProductVariants.GetActiveForRentalAsync(variantIds, cancellationToken))
            .ToDictionary(variant => variant.VariantId);
        var bundles = (await _unitOfWork.ProductBundles.GetActiveForRentalAsync(bundleIds, cancellationToken))
            .ToDictionary(bundle => bundle.BundleId);

        var resolved = new List<ResolvedRentalLineResult>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.ProductVariantId.HasValue)
            {
                if (!variants.TryGetValue(line.ProductVariantId.Value, out var variant) || !variant.IsActive)
                    throw new NotFoundException("Thiết bị không tồn tại hoặc đã ngừng kinh doanh", ErrorCodes.ItemNotFound);

                var name = DisplayName(variant.ProductName, variant.VariantName);
                resolved.Add(new ResolvedRentalLineResult
                {
                    Index = index,
                    ItemType = RentalItemTypes.Variant,
                    ProductVariantId = variant.VariantId,
                    DisplayName = name,
                    Quantity = line.Quantity,
                    Components =
                    [
                        new RentalLineComponentItem
                        {
                            ProductVariantId = variant.VariantId,
                            VariantName = name,
                            QuantityPerUnit = 1,
                            TotalQuantity = line.Quantity
                        }
                    ]
                });
                continue;
            }

            var bundleId = line.BundleId!.Value;
            if (!bundles.TryGetValue(bundleId, out var bundle) || !bundle.IsActive)
                throw new NotFoundException("Combo không tồn tại hoặc đã ngừng kinh doanh", ErrorCodes.BundleNotFound);
            if (bundle.Components.Count == 0)
                throw new NotFoundException("Combo chưa có thiết bị thành phần", ErrorCodes.BundleNotFound);
            if (bundle.Components.Any(component => !component.IsActive))
                throw new NotFoundException("Combo có thiết bị thành phần đã ngừng kinh doanh", ErrorCodes.ItemNotFound);

            resolved.Add(new ResolvedRentalLineResult
            {
                Index = index,
                ItemType = RentalItemTypes.Bundle,
                BundleId = bundle.BundleId,
                DisplayName = bundle.Name,
                Quantity = line.Quantity,
                Components = bundle.Components.Select(component => new RentalLineComponentItem
                {
                    ProductVariantId = component.VariantId,
                    VariantName = DisplayName(component.ProductName, component.VariantName),
                    QuantityPerUnit = component.QuantityPerBundle,
                    TotalQuantity = component.QuantityPerBundle * line.Quantity
                }).ToList()
            });
        }

        return resolved;
    }

    public async Task<List<RentalLineAvailabilityResult>> EvaluateAsync(RentalPeriodResult period, IReadOnlyList<ResolvedRentalLineResult> lines, CancellationToken cancellationToken = default)
    {
        var demand = RentalAvailabilityCalculator.AggregateDemand(lines);
        var filter = await RentalAvailabilityFilterFactory.CreateAsync(_unitOfWork, period, UtcNow, cancellationToken);
        var available = await _unitOfWork.EquipmentAssets.GetAvailableCountsAsync(demand.Keys.ToList(), filter, cancellationToken);

        return RentalAvailabilityCalculator.BuildLineResults(lines, demand, available);
    }

    public async Task<Dictionary<Guid, List<Guid>>> PickAssetsAsync(RentalPeriodResult period, IReadOnlyDictionary<Guid, int> demandByVariant, CancellationToken cancellationToken = default)
    {
        var filter = await RentalAvailabilityFilterFactory.CreateAsync(_unitOfWork, period, UtcNow, cancellationToken);

        var picks = new Dictionary<Guid, List<Guid>>();
        foreach (var (variantId, quantity) in demandByVariant.Where(demand => demand.Value > 0).OrderBy(demand => demand.Key))
        {
            var assetIds = await _unitOfWork.EquipmentAssets.GetAvailableAssetIdsAsync(variantId, filter, quantity, cancellationToken);
            if (assetIds.Count < quantity)
            {
                throw new ConflictException(
                    $"Không đủ thiết bị khả dụng (cần {quantity}, còn {assetIds.Count})",
                    ErrorCodes.ResourceUnavailable);
            }

            picks[variantId] = assetIds;
        }

        return picks;
    }

    public Task LockVariantsAsync(IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default)
        => _unitOfWork.EquipmentAssets.AcquireVariantLocksAsync(variantIds, cancellationToken);

    public string BuildLineMessage(RentalLineAvailabilityResult line, RentalPeriodResult period)
    {
        if (line.Shortages.Count == 0)
            return string.Empty;

        var range = FormatRange(period);
        return string.Join(" ", line.Shortages.Select(shortage => shortage.Available > 0
            ? $"Chỉ còn {shortage.Available} {shortage.VariantName} trong {range}."
            : $"{shortage.VariantName} đã hết trong {range}."));
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private static string FormatRange(RentalPeriodResult period)
    {
        var start = (period.StartUtc + VietnamOffset).ToString("dd/MM", CultureInfo.InvariantCulture);
        var end = (period.EndUtc + VietnamOffset).ToString("dd/MM", CultureInfo.InvariantCulture);
        return $"{start} – {end}";
    }

    private static string DisplayName(string productName, string variantName) => $"{productName} — {variantName}";

    private static void ValidateShape(RentalLineInput line, int index)
    {
        if (line.ProductVariantId.HasValue == line.BundleId.HasValue)
        {
            throw new BadRequestException(
                $"Dòng {index + 1}: phải có đúng một trong productVariantId hoặc bundleId",
                ErrorCodes.InvalidRentalItem);
        }

        if (line.Quantity <= 0)
            throw new BadRequestException($"Dòng {index + 1}: số lượng phải lớn hơn 0", ErrorCodes.InvalidRentalItem);
    }
}
