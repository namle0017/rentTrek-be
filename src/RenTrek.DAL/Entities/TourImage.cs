namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể tour image trong mô hình dữ liệu RenTrek.
/// </summary>
public class TourImage : BaseEntity
{
    public Guid TourId { get; set; }
    public Tour Tour { get; set; } = null!;

    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}
