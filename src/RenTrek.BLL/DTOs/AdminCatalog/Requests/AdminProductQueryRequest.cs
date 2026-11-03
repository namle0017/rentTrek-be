using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

public sealed class AdminProductQueryRequest : PagingRequest
{
    public string? Status { get; set; }
    public Guid? CategoryId { get; set; }
}
