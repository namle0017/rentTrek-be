namespace RenTrek.BLL.DTOs.Warehouse.Requests;

public class IssueTripGearRequest
{
    /// <summary>
    /// Danh sách mã thiết bị cần xuất kho. Nếu rỗng/null, hệ thống sẽ xuất toàn bộ thiết bị đang Reserved của chuyến.
    /// </summary>
    public List<string>? AssetCodes { get; set; }
}
