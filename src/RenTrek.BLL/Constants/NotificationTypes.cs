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
    public const string RentalLateFee = "RentalLateFee";
    public const string RentalNoShow = "RentalNoShow";
    public const string RentalHandedOver = "RentalHandedOver";
    public const string RentalReturnReminder = "RentalReturnReminder";
    public const string RentalReturnOverdue = "RentalReturnOverdue";
    public const string RentalSettlement = "RentalSettlement";
    public const string RefundBankInfoRequired = "RefundBankInfoRequired";
    public const string RentalReportSubmitted = "RentalReportSubmitted";
    public const string CustomerReportSubmitted = "CustomerReportSubmitted";
    public const string CustomerReportUpdated = "CustomerReportUpdated";
}
