namespace RenTrek.DAL.Models;

/// <summary>
/// Thông tin combo thuê cùng các thành phần (chưa xoá) theo thứ tự tạo.
/// </summary>
public class RentalBundleInfo
{
    public Guid BundleId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>True khi combo Active và chưa bị xoá.</summary>
    public bool IsActive { get; set; }

    public List<RentalBundleComponentInfo> Components { get; set; } = [];
}
