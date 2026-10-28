namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Thành phần của combo trong dòng thuê của đơn.
/// </summary>
public class RentalOrderBundleComponentResponse
{
    public string VariantName { get; set; } = string.Empty;

    public int QuantityPerBundle { get; set; }
}
