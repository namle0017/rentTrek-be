using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.Product.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ product query.
/// </summary>
public class ProductQueryRequest : PagingRequest
{
    public Guid? CategoryId { get; set; }
    public string? OfferingType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}
