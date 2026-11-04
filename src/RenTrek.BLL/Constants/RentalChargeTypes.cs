namespace RenTrek.BLL.Constants;

/// <summary>rental_charges.charge_type.</summary>
public static class RentalChargeTypes
{
    public const string LateReturn = "LateReturn";
    public const string Damage = "Damage";
    public const string Loss = "Loss";
    public const string Other = "Other";

    public static readonly string[] All = [LateReturn, Damage, Loss, Other];
}
