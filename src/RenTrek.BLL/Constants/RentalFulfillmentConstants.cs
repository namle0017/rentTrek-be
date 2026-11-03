namespace RenTrek.BLL.Constants;

/// <summary>rental_charges.status (contract 03.1 phụ lục A, PRD §7.6).</summary>
public static class RentalChargeStatuses
{
    /// <summary>Hệ thống / Staff đề xuất, chưa tính vào nghĩa vụ; khách không thấy.</summary>
    public const string Draft = "Draft";
    public const string Assessed = "Assessed";
    public const string Settled = "Settled";
    public const string Voided = "Voided";
}

/// <summary>rental_charges.charge_type.</summary>
public static class RentalChargeTypes
{
    public const string LateReturn = "LateReturn";
    public const string Damage = "Damage";
    public const string Loss = "Loss";
    public const string Other = "Other";

    public static readonly string[] All = [LateReturn, Damage, Loss, Other];
}

/// <summary>asset_assignments.return_condition.</summary>
public static class ReturnConditions
{
    public const string Normal = "Normal";
    public const string Damaged = "Damaged";
    public const string MissingAccessory = "MissingAccessory";
    public const string Lost = "Lost";

    public static readonly string[] All = [Normal, Damaged, MissingAccessory, Lost];
}
