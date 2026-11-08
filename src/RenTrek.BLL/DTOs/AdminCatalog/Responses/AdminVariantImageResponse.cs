namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Ảnh biến thể kèm id để xoá (§13.10).</summary>
public sealed class AdminVariantImageResponse
{
    public Guid ImageId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}
