namespace RenTrek.DAL.Models;

/// <summary>
/// Tham chiếu ngắn tới một đơn (dùng cho cảnh báo).
/// </summary>
public class OrderRefRow
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
}
