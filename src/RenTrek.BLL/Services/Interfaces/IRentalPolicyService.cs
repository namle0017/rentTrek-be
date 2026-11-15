using RenTrek.BLL.DTOs.RentalPolicy.Requests;
using RenTrek.BLL.DTOs.RentalPolicy.Responses;
using RenTrek.BLL.Helpers;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Chính sách thuê đồ: % cọc, giới hạn huỷ miễn phí, bậc phạt trễ nhận / trả.</summary>
public interface IRentalPolicyService
{
    /// <summary>Policy đang Active; nếu DB chưa cấu hình thì dùng mặc định (20% cọc, bậc 1/2/3 và 3/4/5 ngày).</summary>
    Task<RentalPolicySnapshot> GetActiveSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>Policy đã snapshot vào đơn; đơn cũ chưa có snapshot dùng policy đang Active.</summary>
    Task<RentalPolicySnapshot> GetSnapshotAsync(Guid? rentalPolicyId, CancellationToken cancellationToken = default);

    Task<RentalPolicyResponse> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Mọi version cho màn quản trị (version giảm dần) kèm số đơn, người tạo, ngày tạo.</summary>
    Task<IReadOnlyList<AdminRentalPolicyResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Một version cho màn quản trị; NotFoundException nếu không có.</summary>
    Task<AdminRentalPolicyResponse> GetByIdAsync(Guid rentalPolicyId, CancellationToken cancellationToken = default);

    /// <summary>Admin tạo version mới: kích hoạt ngay, version cũ chuyển Archived. Đơn đã đặt giữ policy cũ.</summary>
    Task<RentalPolicyResponse> CreateVersionAsync(Guid adminUserId, CreateRentalPolicyVersionRequest request, CancellationToken cancellationToken = default);

    RentalPolicyResponse ToResponse(RentalPolicySnapshot snapshot);
}
