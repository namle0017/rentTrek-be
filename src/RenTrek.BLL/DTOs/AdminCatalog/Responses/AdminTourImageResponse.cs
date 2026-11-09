namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Một ảnh trong thư viện tour (§6.7).</summary>
public sealed class AdminTourImageResponse
{
    public Guid ImageId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}
