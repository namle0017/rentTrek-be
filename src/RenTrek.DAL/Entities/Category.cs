namespace RenTrek.DAL.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryType { get; set; } = "TOUR";
    public string Status { get; set; } = "Active";

    public ICollection<Tour> Tours { get; set; } = [];
}
