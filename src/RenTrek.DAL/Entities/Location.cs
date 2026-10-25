namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể location trong mô hình dữ liệu RenTrek.
/// </summary>
public class Location : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }

    public ICollection<Tour> Tours { get; set; } = [];
}
