namespace RenTrek.BLL.DTOs.RentalOps.Requests;

/// <summary>Giao thiết bị. Bỏ trống danh sách = giao toàn bộ thiết bị đang giữ cho đơn.</summary>
public sealed class HandoverRentalRequest
{
    public List<Guid>? AssignmentIds { get; set; }
}
