using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.Warehouse.Requests;

public class AssignTripAssetsRequest
{
    [Required(ErrorMessage = "Danh sách mã thiết bị không được để trống.")]
    [MinLength(1, ErrorMessage = "Cần gán ít nhất một mã thiết bị.")]
    public List<string> AssetCodes { get; set; } = [];
}
