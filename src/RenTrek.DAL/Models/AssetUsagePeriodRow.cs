namespace RenTrek.DAL.Models;

/// <summary>
/// Một lần tài sản được phát (Issued / Returned / LostOrException).
/// </summary>
public class AssetUsagePeriodRow
{
    public DateTime AssignedAt { get; set; }
    public DateTime ReservedFrom { get; set; }
    public DateTime? ReturnedAt { get; set; }
}
