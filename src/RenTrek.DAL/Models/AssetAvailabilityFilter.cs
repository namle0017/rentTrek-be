namespace RenTrek.DAL.Models;

/// <summary>
/// Tham số tính tài sản khả dụng trong một khoảng thời gian thuê (đã cộng sẵn thời gian đệm vệ sinh).
/// Các giá trị trạng thái là chuỗi do BLL truyền vào (xem RenTrek.BLL.Constants).
/// </summary>
public class AssetAvailabilityFilter
{
    /// <summary>Đầu khoảng cần kiểm tra (UTC).</summary>
    public DateTime WindowStart { get; set; }

    /// <summary>Cuối khoảng cần kiểm tra (UTC), đã gồm đệm vệ sinh.</summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>Thời điểm hiện tại (UTC), dùng cho quy tắc tài sản đã giao nhưng quá hạn chưa trả (BR-20).</summary>
    public DateTime NowUtc { get; set; }

    /// <summary>Các equipment_assets.asset_status được phép cho thuê.</summary>
    public IReadOnlyCollection<string> RentableAssetStatuses { get; set; } = [];

    /// <summary>Các asset_assignments.status làm tài sản bị chiếm trong khoảng giao nhau.</summary>
    public IReadOnlyCollection<string> BlockingAssignmentStatuses { get; set; } = [];

    /// <summary>Giá trị asset_assignments.status của phân bổ đã giao cho khách.</summary>
    public string IssuedStatus { get; set; } = string.Empty;
}
