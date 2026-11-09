namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Một bậc giá thuê theo ngày; MaxDays null = từ MinDays ngày trở lên.</summary>
public sealed class PriceTierRequest
{
    public int MinDays { get; set; }
    public int? MaxDays { get; set; }
    public decimal DailyPrice { get; set; }
}
