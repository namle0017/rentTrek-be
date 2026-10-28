namespace RenTrek.BLL.Constants;

/// <summary>
/// Khoá system_configurations dùng cho luồng thuê thiết bị và giá trị mặc định khi thiếu cấu hình.
/// </summary>
public static class RentalConfigKeys
{
    public const string HoldMinutes = "rental.holdMinutes";
    public const string CleaningBufferHours = "rental.cleaningBufferHours";
    public const string IdempotencyTtlHours = "idempotency.ttlHours";

    public const int DefaultHoldMinutes = 15;
    public const decimal DefaultCleaningBufferHours = 2;
    public const int DefaultIdempotencyTtlHours = 24;
}
