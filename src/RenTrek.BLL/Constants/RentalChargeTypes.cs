namespace RenTrek.BLL.Constants;

/// <summary>rental_charges.charge_type.</summary>
public static class RentalChargeTypes
{
    public const string LateReturn = "LateReturn";
    public const string Damage = "Damage";
    public const string Loss = "Loss";
    public const string Other = "Other";

    /// <summary>Phí gia hạn đơn thuê do hệ thống tạo khi khách đồng ý gia hạn; không cho Staff lập tay nên không nằm trong <see cref="All"/>.</summary>
    public const string Extension = "Extension";

    public static readonly string[] All = [LateReturn, Damage, Loss, Other];
}
