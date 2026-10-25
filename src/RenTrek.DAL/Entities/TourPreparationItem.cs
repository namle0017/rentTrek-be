namespace RenTrek.DAL.Entities;

public class TourPreparationItem
{
    public Guid PreparationItemId { get; set; }
    public Guid TourId { get; set; }
    public string ItemName { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Tour Tour { get; set; } = null!;
}
