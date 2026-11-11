namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ đếm KPI quản trị.
/// </summary>
public class AdminKpiCountsRow
{
    /// <summary>Số đơn đặt chuyến tạo trong khoảng thời gian.</summary>
    public int BookingCount { get; set; }

    /// <summary>Số tài sản (distinct) đang Issued.</summary>
    public int IssuedAssetCount { get; set; }

    /// <summary>Số tài sản chưa xoá đang Available.</summary>
    public int AvailableAssetCount { get; set; }
}
