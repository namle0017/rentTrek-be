namespace RenTrek.BLL.DTOs.Product;

public class ProductResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public decimal? PricePerDay { get; set; }
    public bool IsRentable => PricePerDay.HasValue;
    public DateTime CreatedAt { get; set; }
}
