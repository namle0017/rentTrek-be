using RenTrek.BLL.Constants;
using RenTrek.BLL.Exceptions;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Helpers;

/// <summary>Quy tắc vận hành đơn thuê tách khỏi service để test không cần DB.</summary>
public static class RentalFulfillmentRules
{
    /// <summary>FR-R05: Staff chỉ nhận đơn chưa có ai hoặc đơn của chính mình; Admin nhận lại được đơn của người khác.</summary>
    public static bool CanClaim(Guid? handledByUserId, Guid actorId, bool isAdmin)
        => handledByUserId is null || handledByUserId == actorId || isAdmin;

    /// <summary>FR-C04: phí Draft Staff/Admin đều hủy được; phí Assessed đã thành công nợ của khách nên chỉ Admin hủy.</summary>
    public static bool CanVoid(string chargeStatus, bool isAdmin)
        => chargeStatus == RentalChargeStatuses.Draft
           || (chargeStatus == RentalChargeStatuses.Assessed && isAdmin);

    /// <summary>Giao theo danh sách chọn; bỏ trống = giao mọi thiết bị đang giữ cho đơn.</summary>
    public static List<AssetAssignment> PickForHandover(List<AssetAssignment> reserved, IReadOnlyCollection<Guid>? assignmentIds)
    {
        if (assignmentIds is not { Count: > 0 })
            return reserved;

        var targets = reserved.Where(a => assignmentIds.Contains(a.AssetAssignmentId)).ToList();
        if (targets.Count != assignmentIds.Distinct().Count())
            throw new ConflictException("Có thiết bị không ở trạng thái chờ giao");
        return targets;
    }

    /// <summary>FR-R06 quét / nhập mã: chỉ nhận thiết bị đang giữ cho chính đơn này; mã đã giao không còn trong danh sách nên không giao trùng.</summary>
    public static List<AssetAssignment> PickByCode(List<AssetAssignment> reserved, string assetCode)
    {
        var code = assetCode.Trim();
        var match = reserved.FirstOrDefault(a => string.Equals(a.EquipmentAsset.AssetCode, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new ConflictException($"Thiết bị {code} không nằm trong danh sách chờ giao của đơn này");
        return [match];
    }
}
