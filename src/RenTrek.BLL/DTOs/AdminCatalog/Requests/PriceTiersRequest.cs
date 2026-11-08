namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body thay toàn bộ bảng bậc giá của biến thể / combo (§13.12, §13.21).</summary>
public sealed class PriceTiersRequest
{
    public List<PriceTierRequest> Tiers { get; set; } = [];
}
