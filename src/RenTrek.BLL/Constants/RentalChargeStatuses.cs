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
