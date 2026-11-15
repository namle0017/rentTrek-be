namespace RenTrek.BLL.Constants;

/// <summary>refund_cases.source.</summary>
public static class RefundCaseSources
{
    public const string TripCancellation = "TripCancellation";
    public const string RentalCancellation = "RentalCancellation";
    public const string RentalNoShow = "RentalNoShow";
    public const string RentalDepositReturn = "RentalDepositReturn";
    public const string TripPartialCancellation = "TripPartialCancellation";
    public const string TripReschedule = "TripReschedule";
    public static readonly IReadOnlyList<string> All =
        [TripCancellation, RentalCancellation, RentalNoShow, RentalDepositReturn, TripPartialCancellation, TripReschedule];
}
