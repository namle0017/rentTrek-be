namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị equipment_assets.asset_status. Schema (varchar) không liệt kê miền giá trị, nên chỉ các trạng thái trong
/// <see cref="Rentable"/> được coi là cho thuê được; mọi giá trị khác (Maintenance, Damaged, Lost, Retired...) bị loại.
/// </summary>
public static class EquipmentAssetStatuses
{
    public const string Available = "Available";
    public const string Good = "Good";
    public const string Maintenance = "Maintenance";
    public const string Damaged = "Damaged";
    public const string Lost = "Lost";
    public const string Retired = "Retired";

    /// <summary>Các asset_status được phép cho thuê.</summary>
    public static readonly IReadOnlyList<string> Rentable = [Available, Good];
}
