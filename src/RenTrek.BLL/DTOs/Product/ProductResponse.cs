namespace RenTrek.BLL.DTOs.Product;

public class ProductResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public decimal? PricePerDay { get; set; }
    public decimal? SalePrice { get; set; }
    public bool IsRentable => PricePerDay.HasValue;
    public bool IsForSale => SalePrice.HasValue;
    public DateTime CreatedAt { get; set; }
}
