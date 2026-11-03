using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.Warehouse.Requests;

public class AssetQueryRequest : PagingRequest
{
    public Guid? ProductVariantId { get; set; }
    public string? AssetStatus { get; set; }
}
