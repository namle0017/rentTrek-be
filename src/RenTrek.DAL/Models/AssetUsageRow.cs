namespace RenTrek.DAL.Models;

/// <summary>
/// Một tài sản và các lần được phát trong khoảng báo cáo hiệu suất.
/// </summary>
public class AssetUsageRow
{
    public Guid EquipmentAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public List<AssetUsagePeriodRow> Periods { get; set; } = [];
}
