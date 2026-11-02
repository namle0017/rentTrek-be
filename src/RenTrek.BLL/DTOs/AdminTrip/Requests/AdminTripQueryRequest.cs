using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Tham số lọc và phân trang danh sách chuyến đi cho Admin/Staff (8.1).
/// </summary>
public sealed class AdminTripQueryRequest : PagingRequest
{
    public Guid? TourId { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? FromStartAt { get; set; }
    public DateTimeOffset? ToStartAt { get; set; }
    public Guid? ManagedByUserId { get; set; }
}
