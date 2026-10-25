namespace RenTrek.DAL.Entities;

public class TourImage
{
    public Guid TourImageId { get; set; }
    public Guid TourId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Tour Tour { get; set; } = null!;
}
