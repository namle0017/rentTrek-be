namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body tạo / sửa thông tin chung của tour (§6.2, §6.4).</summary>
public sealed class SaveTourRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public Guid MainLocationId { get; set; }
    public string? Description { get; set; }
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public int DurationMinutes { get; set; }
    public decimal BasePrice { get; set; }
}
