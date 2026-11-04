using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.RentalOps.Requests;

/// <summary>Bộ lọc danh sách đơn thuê ở màn vận hành.</summary>
public sealed class AdminRentalOrderQueryRequest : PagingRequest
{
    public string? Status { get; set; }

    /// <summary>Chỉ đơn còn thiết bị đang ở khách đã quá hạn trả.</summary>
    public bool OverdueOnly { get; set; }
}
