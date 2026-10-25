namespace RenTrek.DAL.Entities;

public class Tour
{
    public Guid TourId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid MainLocationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DurationDays { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public decimal BasePrice { get; set; }
    public int DurationMinutes { get; set; }
    public int DurationNights { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid TourFamilyId { get; set; }
    public int Version { get; set; }
    public DateTime? ItineraryLockedAt { get; set; }

    public Category Category { get; set; } = null!;
    public Account CreatedByUser { get; set; } = null!;
    public Location MainLocation { get; set; } = null!;
    public ICollection<TourActivity> TourActivities { get; set; } = new List<TourActivity>();
    public ICollection<TourImage> TourImages { get; set; } = new List<TourImage>();
    public ICollection<TourPost> TourPosts { get; set; } = new List<TourPost>();
    public ICollection<TourPreparationItem> TourPreparationItems { get; set; } = new List<TourPreparationItem>();
    public ICollection<TourProvidedItem> TourProvidedItems { get; set; } = new List<TourProvidedItem>();
    public ICollection<TourRecurrenceRule> TourRecurrenceRules { get; set; } = new List<TourRecurrenceRule>();
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
