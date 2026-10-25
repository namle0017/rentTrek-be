namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể tour preparation item trong mô hình dữ liệu RenTrek.
/// </summary>
public class TourPreparationItem : BaseEntity
{
    public Guid TourId { get; set; }
    public Tour Tour { get; set; } = null!;

    public string ItemName { get; set; } = string.Empty;
    public string? Note { get; set; }
}
