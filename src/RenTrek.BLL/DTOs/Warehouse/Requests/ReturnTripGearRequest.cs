using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.Warehouse.Requests;

public class ReturnTripGearRequest
{
    [Required(ErrorMessage = "Danh sách thiết bị hoàn trả không được để trống.")]
    [MinLength(1, ErrorMessage = "Cần thu hồi ít nhất một thiết bị.")]
    public List<TripAssetReturnItemRequest> Returns { get; set; } = [];
}
