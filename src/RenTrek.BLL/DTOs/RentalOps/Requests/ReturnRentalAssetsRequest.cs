using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.RentalOps.Requests;

/// <summary>Nhận trả một hoặc nhiều thiết bị đang ở khách.</summary>
public sealed class ReturnRentalAssetsRequest
{
    public List<ReturnRentalAssetItemRequest> Items { get; set; } = [];
}
