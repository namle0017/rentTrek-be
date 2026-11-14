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

    /// <summary>Phí trễ nhận theo bậc % cọc (hệ thống tạo, chỉ nâng bậc).</summary>
    public const string LatePickup = "LatePickup";

    /// <summary>Phạt trả trễ theo bậc % cọc, cộng thêm tiền thuê ngày trễ (LateReturn).</summary>
    public const string LateReturnPenalty = "LateReturnPenalty";

    /// <summary>Phí huỷ (vượt số lần huỷ miễn phí hoặc huỷ khi đã trễ nhận).</summary>
    public const string CancellationFee = "CancellationFee";

    /// <summary>Loại phí chỉ có một dòng hiệu lực / đơn, hệ thống nâng bậc.</summary>
    public static readonly string[] Tiered = [LatePickup, LateReturnPenalty, CancellationFee];

    public static readonly string[] All = [LateReturn, Damage, Loss, Other];
}
