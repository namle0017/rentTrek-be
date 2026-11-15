namespace RenTrek.BLL.Constants;

/// <summary>rental_orders.cancellation_type.</summary>
public static class RentalCancellationTypes
{
    /// <summary>Huỷ đơn chưa thanh toán.</summary>
    public const string Unpaid = "Unpaid";

    /// <summary>Huỷ trong hạn miễn phí (trước giờ nhận + 24h), hoàn 100%.</summary>
    public const string FreeCancel = "FreeCancel";

    /// <summary>Huỷ trong hạn miễn phí nhưng đã vượt số lần huỷ miễn phí → mất % cọc.</summary>
    public const string OverLimitCancel = "OverLimitCancel";

    /// <summary>Huỷ khi đã trễ nhận (bậc 5% / 20% cọc).</summary>
    public const string LateCancel = "LateCancel";

    /// <summary>Khách không đến nhận: mất 100% cọc, hệ thống / staff huỷ.</summary>
    public const string NoShow = "NoShow";

    public const string Admin = "Admin";

    /// <summary>Các lần huỷ được tính vào giới hạn huỷ miễn phí.</summary>
    public static readonly IReadOnlyList<string> CountedAsFreeCancel = [FreeCancel, OverLimitCancel];
}
