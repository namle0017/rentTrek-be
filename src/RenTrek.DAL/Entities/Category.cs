namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể category trong mô hình dữ liệu RenTrek.
/// </summary>
public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryType { get; set; } = "TOUR";
    public string Status { get; set; } = "Active";

    public ICollection<Tour> Tours { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}
