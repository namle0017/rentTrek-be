namespace RenTrek.DAL.Entities;

public class Location
{
    public Guid LocationId { get; set; }
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Tour> Tours { get; set; } = new List<Tour>();
}
