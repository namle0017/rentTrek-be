namespace RenTrek.DAL.Models;

/// <summary>
/// Một đồ cấp của chuyến sắp khởi hành còn thiếu tài sản được giữ / phát.
/// </summary>
public class MissingGearRow
{
    public Guid TripId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public int RequiredQuantity { get; set; }

    /// <summary>Số tài sản đang Reserved hoặc Issued.</summary>
    public int AssignedQuantity { get; set; }
}
