namespace RenTrek.DAL.Models;

/// <summary>
/// Địa điểm chưa xoá kèm số tour chưa xoá đang dùng làm điểm chính.
/// </summary>
public class AdminLocationRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int TourCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
