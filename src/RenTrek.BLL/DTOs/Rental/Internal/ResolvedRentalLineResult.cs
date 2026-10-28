namespace RenTrek.BLL.DTOs.Rental.Internal;

/// <summary>
/// Dòng thuê đã kiểm tra hợp lệ và nạp thông tin hàng hoá (chưa tính giá).
/// </summary>
public class ResolvedRentalLineResult
{
    /// <summary>Chỉ số dòng trong danh sách đầu vào (0-based).</summary>
    public int Index { get; set; }

    /// <summary>Variant hoặc Bundle (xem RentalItemTypes).</summary>
    public string ItemType { get; set; } = string.Empty;

    public Guid? ProductVariantId { get; set; }
    public Guid? BundleId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public List<RentalLineComponentItem> Components { get; set; } = [];
}
