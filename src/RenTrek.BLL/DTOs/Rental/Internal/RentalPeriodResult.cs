namespace RenTrek.BLL.DTOs.Rental.Internal;

/// <summary>
/// Khoảng thuê đã chuẩn hoá về UTC, kèm số ngày tính tiền.
/// </summary>
public class RentalPeriodResult
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int BillableDays { get; set; }
}
