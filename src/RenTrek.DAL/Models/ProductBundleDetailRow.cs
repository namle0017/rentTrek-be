namespace RenTrek.DAL.Models;

/// <summary>
/// Chi tiết combo: thông tin chung, thành phần kèm ảnh và các bậc giá.
/// </summary>
public class ProductBundleDetailRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<ProductBundleComponentRow> Components { get; set; } = [];

    /// <summary>Bậc giá chưa xoá, sắp theo MinDays tăng dần.</summary>
    public List<RentalPriceTierRow> PriceTiers { get; set; } = [];
}
