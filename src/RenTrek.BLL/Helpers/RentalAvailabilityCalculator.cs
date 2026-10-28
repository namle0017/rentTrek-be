using RenTrek.BLL.DTOs.Rental.Internal;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Tính khả dụng thiết bị cho giỏ thuê (thuần, không truy cập DB).
/// </summary>
public static class RentalAvailabilityCalculator
{
    /// <summary>Cộng dồn nhu cầu theo biến thể: dòng lẻ và (SL combo × SL thành phần) của mọi dòng.</summary>
    public static Dictionary<Guid, int> AggregateDemand(IEnumerable<ResolvedRentalLineResult> lines)
    {
        var demand = new Dictionary<Guid, int>();
        foreach (var component in lines.SelectMany(l => l.Components))
        {
            demand[component.ProductVariantId] =
                demand.GetValueOrDefault(component.ProductVariantId) + component.TotalQuantity;
        }

        return demand;
    }

    /// <summary>
    /// Số combo tối đa thuê được = min ⌊khả dụng(v_i) / SL thành phần_i⌋, kèm thành phần giới hạn
    /// (cho giá trị nhỏ nhất, hoà thì thành phần xuất hiện trước). Không có thành phần thì trả (0, null).
    /// </summary>
    public static (int Quantity, Guid? LimitingVariantId) BundleAvailability(
        IEnumerable<RentalLineComponentItem> components,
        IReadOnlyDictionary<Guid, int> availableByVariant)
    {
        var best = int.MaxValue;
        Guid? limiting = null;
        foreach (var component in components)
        {
            var fit = component.QuantityPerUnit <= 0
                ? 0
                : availableByVariant.GetValueOrDefault(component.ProductVariantId) / component.QuantityPerUnit;
            if (fit < best)
            {
                best = fit;
                limiting = component.ProductVariantId;
            }
        }

        return limiting is null ? (0, null) : (best, limiting);
    }

    /// <summary>Kết quả từng dòng, dựa trên tổng nhu cầu cộng dồn và số khả dụng theo biến thể.</summary>
    public static List<RentalLineAvailabilityResult> BuildLineResults(
        IReadOnlyList<ResolvedRentalLineResult> lines,
        IReadOnlyDictionary<Guid, int> demandByVariant,
        IReadOnlyDictionary<Guid, int> availableByVariant)
    {
        var results = new List<RentalLineAvailabilityResult>(lines.Count);
        foreach (var line in lines)
        {
            var shortages = line.Components
                .Where(c => demandByVariant.GetValueOrDefault(c.ProductVariantId)
                            > availableByVariant.GetValueOrDefault(c.ProductVariantId))
                .Select(c => new RentalAvailabilityShortageItem
                {
                    ProductVariantId = c.ProductVariantId,
                    VariantName = c.VariantName,
                    Requested = demandByVariant[c.ProductVariantId],
                    Available = availableByVariant.GetValueOrDefault(c.ProductVariantId)
                })
                .ToList();

            int availableQuantity;
            RentalAvailabilityShortageItem? limitingComponent = null;
            if (line.BundleId.HasValue)
            {
                var (quantity, limitingVariantId) = BundleAvailability(line.Components, availableByVariant);
                availableQuantity = quantity;
                if (limitingVariantId.HasValue)
                {
                    var component = line.Components.First(c => c.ProductVariantId == limitingVariantId.Value);
                    limitingComponent = new RentalAvailabilityShortageItem
                    {
                        ProductVariantId = component.ProductVariantId,
                        VariantName = component.VariantName,
                        Requested = component.QuantityPerUnit,
                        Available = availableByVariant.GetValueOrDefault(component.ProductVariantId)
                    };
                }
            }
            else
            {
                availableQuantity = line.Components.Count == 0
                    ? 0
                    : availableByVariant.GetValueOrDefault(line.Components[0].ProductVariantId);
            }

            results.Add(new RentalLineAvailabilityResult
            {
                LineIndex = line.Index,
                IsAvailable = shortages.Count == 0,
                AvailableQuantity = availableQuantity,
                Shortages = shortages,
                LimitingComponent = limitingComponent
            });
        }

        return results;
    }
}
