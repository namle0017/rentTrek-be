using RenTrek.BLL.Constants;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Kiểm tra thứ tự và thời điểm chuyển trạng thái Trip (PRD §7.2) cho các bước Staff/Admin bấm tay.
/// Open / ClosedForBooking / Draft đã được kiểm tra riêng trong TripService.
/// </summary>
public static class TripStatusTimeRules
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>Trả thông báo lỗi tiếng Việt nếu không được chuyển; null nếu hợp lệ.</summary>
    public static string? Validate(
        string previousStatus,
        string targetStatus,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        DateTimeOffset now)
    {
        switch (targetStatus)
        {
            case TripStatuses.InProgress:
                if (!Is(previousStatus, TripStatuses.ClosedForBooking))
                    return $"Chỉ bắt đầu chuyến từ trạng thái Đã đóng bán (hiện tại: {previousStatus})";
                if (now < startAt)
                    return $"Chưa tới giờ khởi hành ({Format(startAt)})";
                return null;

            case TripStatuses.Completed:
                if (!Is(previousStatus, TripStatuses.InProgress))
                    return $"Chỉ kết thúc chuyến đang diễn ra (hiện tại: {previousStatus})";
                if (now < endAt)
                    return $"Chưa tới giờ kết thúc chuyến ({Format(endAt)})";
                return null;

            case TripStatuses.Cancelled:
                if (!Is(previousStatus, TripStatuses.Draft)
                    && !Is(previousStatus, TripStatuses.Open)
                    && !Is(previousStatus, TripStatuses.ClosedForBooking))
                    return $"Không thể hủy chuyến đang diễn ra hoặc đã kết thúc (hiện tại: {previousStatus})";
                return null;

            default:
                return null;
        }
    }

    private static bool Is(string status, string expected)
        => string.Equals(status, expected, StringComparison.OrdinalIgnoreCase);

    private static string Format(DateTimeOffset value) => value.ToOffset(VietnamOffset).ToString("dd/MM/yyyy HH:mm");
}
