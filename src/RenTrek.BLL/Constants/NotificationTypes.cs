namespace RenTrek.BLL.Constants;

/// <summary>
/// Hằng số phân loại thông báo trong ứng dụng gửi tới người dùng.
/// </summary>
public static class NotificationTypes
{
    public const string OrderPlaced = "OrderPlaced";
    public const string OrderConfirmed = "OrderConfirmed";
    public const string OrderCancelled = "OrderCancelled";
    public const string TripCancelled = "TripCancelled";
    public const string TripScheduleChanged = "TripScheduleChanged";
    public const string RefundResult = "RefundResult";
    public const string RentalExtended = "RentalExtended";
}
