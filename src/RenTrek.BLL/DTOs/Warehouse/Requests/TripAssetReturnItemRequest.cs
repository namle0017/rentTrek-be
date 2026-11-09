using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.Warehouse.Requests;

public class TripAssetReturnItemRequest
{
    [Required(ErrorMessage = "Mã thiết bị không được để trống.")]
    public string AssetCode { get; set; } = string.Empty;

    /// <summary>
    /// Normal, Damaged, MissingAccessory, Lost
    /// </summary>
    public string ReturnCondition { get; set; } = "Normal";

    public string? Note { get; set; }
}
