namespace RenTrek.BLL.Constants;

/// <summary>payments.purpose.</summary>
public static class PaymentPurposes
{
    public const string TourFee = "TourFee";

    /// <summary>Thanh toán đơn thuê: tiền thuê + cọc trong một lần chuyển khoản.</summary>
    public const string RentalFee = "RentalFee";

    /// <summary>Thu thêm khi phí vượt cọc.</summary>
    public const string AdditionalCharge = "AdditionalCharge";

    /// <summary>Thanh toán gia hạn đơn thuê.</summary>
    public const string RentalExtension = "RentalExtension";

    /// <summary>Trả chênh lệch khi đổi lịch booking sang chuyến đắt hơn.</summary>
    public const string TripReschedule = "TripReschedule";

    /// <summary>Khoản thu sau khi đơn đã thanh toán (không tính vào "đã trả tiền đơn").</summary>
    public static readonly IReadOnlyList<string> AfterOrderPaid = [AdditionalCharge, RentalExtension, TripReschedule];
}
