using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.Product.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ product query.
/// </summary>
public class ProductQueryRequest : PagingRequest
{
    public Guid? CategoryId { get; set; }
}
