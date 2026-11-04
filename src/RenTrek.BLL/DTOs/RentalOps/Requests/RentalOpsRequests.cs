using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.RentalOps.Requests;

/// <summary>Bộ lọc danh sách đơn thuê ở màn vận hành.</summary>
public sealed class AdminRentalOrderQueryRequest : PagingRequest
{
    public string? Status { get; set; }

    /// <summary>Chỉ đơn còn thiết bị đang ở khách đã quá hạn trả.</summary>
    public bool OverdueOnly { get; set; }
}

/// <summary>Giao thiết bị. Bỏ trống danh sách = giao toàn bộ thiết bị đang giữ cho đơn.</summary>
public sealed class HandoverRentalRequest
{
    public List<Guid>? AssignmentIds { get; set; }
}

/// <summary>Quét / nhập mã một thiết bị để giao (FR-R06).</summary>
public sealed class ScanHandoverRequest
{
    public string AssetCode { get; set; } = string.Empty;
}

/// <summary>Nhận trả một hoặc nhiều thiết bị đang ở khách.</summary>
public sealed class ReturnRentalAssetsRequest
{
    public List<ReturnRentalAssetItemRequest> Items { get; set; } = [];
}

public sealed class ReturnRentalAssetItemRequest
{
    public Guid AssignmentId { get; set; }

    /// <summary>Normal | Damaged | MissingAccessory | Lost</summary>
    public string Condition { get; set; } = string.Empty;

    public string? Note { get; set; }
}

/// <summary>Staff lập phí phát sinh (trạng thái Assessed ngay).</summary>
public sealed class CreateRentalChargeRequest
{
    /// <summary>LateReturn | Damage | Loss | Other</summary>
    public string ChargeType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Thiết bị chịu phí; bắt buộc với Damage / Loss.</summary>
    public Guid? AssignmentId { get; set; }
}

public sealed class VoidRentalChargeRequest
{
    public string Reason { get; set; } = string.Empty;
}
