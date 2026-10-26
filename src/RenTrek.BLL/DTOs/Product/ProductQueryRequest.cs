using RenTrek.BLL.DTOs.Common;

namespace RenTrek.BLL.DTOs.Product;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ product query.
/// </summary>
public class ProductQueryRequest : PagingRequest
{
    public Guid? CategoryId { get; set; }
}
