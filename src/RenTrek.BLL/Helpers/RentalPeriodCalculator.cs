using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Rental;
using RenTrek.BLL.Exceptions;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Tính và kiểm tra khoảng thời gian thuê (thuần, không truy cập DB, không đọc đồng hồ hệ thống).
/// </summary>
public static class RentalPeriodCalculator
{
    /// <summary>Số ngày thuê tối đa cho một đơn.</summary>
    public const int MaxRentalDays = 60;

    /// <summary>Cho phép lệch đồng hồ client/server khi so thời điểm nhận với now.</summary>
    public static readonly TimeSpan StartSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Chuẩn hoá về UTC và kiểm tra: trả sau nhận, nhận không ở quá khứ (cho phép lệch 5 phút), tối đa 60 ngày.
    /// Vi phạm thì ném BadRequestException với ErrorCodes.InvalidRentalPeriod.
    /// </summary>
    public static RentalPeriod Validate(DateTime start, DateTime end, DateTime now)
    {
        var startUtc = ToUtc(start);
        var endUtc = ToUtc(end);
        var nowUtc = ToUtc(now);

        if (endUtc <= startUtc)
            throw Invalid("Thời điểm trả phải sau thời điểm nhận");

        if (startUtc < nowUtc - StartSkew)
            throw Invalid("Thời điểm nhận không được ở quá khứ");

        var billableDays = CalculateBillableDays(startUtc, endUtc);
        if (billableDays > MaxRentalDays)
            throw Invalid($"Thời gian thuê tối đa {MaxRentalDays} ngày");

        return new RentalPeriod
        {
            StartUtc = startUtc,
            EndUtc = endUtc,
            BillableDays = billableDays
        };
    }

    /// <summary>Số ngày tính tiền = max(1, ceil((end - start) / 24h)). Không kiểm tra hợp lệ.</summary>
    public static int CalculateBillableDays(DateTime start, DateTime end)
    {
        var ticks = (end - start).Ticks;
        if (ticks <= 0)
            return 1;

        var days = (ticks + TimeSpan.TicksPerDay - 1) / TimeSpan.TicksPerDay;
        return (int)Math.Max(1, Math.Min(days, int.MaxValue));
    }

    /// <summary>Chuẩn hoá về UTC (Kind Unspecified được coi là UTC) vì Npgsql timestamptz yêu cầu Kind = Utc.</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static BadRequestException Invalid(string message)
        => new(message, ErrorCodes.InvalidRentalPeriod);
}
