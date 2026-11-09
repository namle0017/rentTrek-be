namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body sắp lại thứ tự ảnh và đặt ảnh đại diện (§6.9). Danh sách phải đủ mọi ảnh của tour.</summary>
public sealed class ReorderTourImagesRequest
{
    public List<Guid> ImageIdsInOrder { get; set; } = [];
    public Guid? PrimaryImageId { get; set; }
}
