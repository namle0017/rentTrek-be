namespace RenTrek.DAL.Entities;

public class TourPreparationItem : BaseEntity
{
    public Guid TourId { get; set; }
    public Tour Tour { get; set; } = null!;

    public string ItemName { get; set; } = string.Empty;
    public string? Note { get; set; }
}
