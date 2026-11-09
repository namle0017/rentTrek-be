namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng danh sách tour cho vận hành. Status là giá trị thô của tours.status.
/// </summary>
public class AdminTourRow
{
    public Guid TourId { get; set; }
    public Guid TourFamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string MainLocationName { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public decimal BasePrice { get; set; }
    public int Version { get; set; }
    public bool IsItineraryLocked { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>Ảnh chính, hoặc ảnh có thứ tự nhỏ nhất.</summary>
    public string? PrimaryImageUrl { get; set; }

    /// <summary>Chuyến chưa xoá bắt đầu sau <see cref="AdminTourFilter.Now"/>.</summary>
    public int UpcomingTripsCount { get; set; }
}
