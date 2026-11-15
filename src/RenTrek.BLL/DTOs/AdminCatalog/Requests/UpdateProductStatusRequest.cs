namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Yêu cầu đổi nhanh trạng thái kinh doanh của sản phẩm cha (Active / Inactive).</summary>
public sealed class UpdateProductStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
