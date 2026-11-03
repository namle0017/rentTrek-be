namespace RenTrek.BLL.Constants;

/// <summary>
/// Khoá system_configurations dùng cho luồng thuê thiết bị và giá trị mặc định khi thiếu cấu hình.
/// </summary>
public static class RentalConfigKeys
{
    public const string HoldMinutes = "rental.holdMinutes";
    public const string CleaningBufferHours = "rental.cleaningBufferHours";
    public const string IdempotencyTtlHours = "idempotency.ttlHours";

    /// <summary>Số giờ ân hạn sau hạn trả trước khi bắt đầu tính phí trễ (đề xuất [D], PRD Q-04).</summary>
    public const string LateGraceHours = "rental.lateGraceHours";
    public const decimal DefaultLateGraceHours = 2;

    public const int DefaultHoldMinutes = 15;
    public const decimal DefaultCleaningBufferHours = 2;
    public const int DefaultIdempotencyTtlHours = 24;
}
