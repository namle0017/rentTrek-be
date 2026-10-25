using RenTrek.BLL.DTOs.Common;

namespace RenTrek.BLL.DTOs.Product;

public class ProductQueryRequest : PagingRequest
{
    public Guid? CategoryId { get; set; }
    public ProductOfferingType? OfferingType { get; set; }
}
