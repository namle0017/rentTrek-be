namespace RenTrek.BLL.DTOs.Rental;

/// <summary>
/// Khoảng thuê đã chuẩn hoá về UTC, kèm số ngày tính tiền.
/// </summary>
public class RentalPeriod
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int BillableDays { get; set; }
}