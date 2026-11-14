using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalPolicy.Requests;
using RenTrek.BLL.DTOs.RentalPolicy.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Chính sách thuê đồ (cọc, huỷ, phạt trễ nhận / trả). Staff/Admin xem; chỉ Admin tạo version mới.
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/rental-policies")]
[Produces("application/json")]
public sealed class AdminRentalPoliciesController(
    IRentalPolicyService rentalPolicyService,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RentalPolicyResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<RentalPolicyResponse>>.Ok(
            await rentalPolicyService.GetAllAsync(cancellationToken), "Lấy danh sách chính sách thuê đồ thành công"));

    /// <summary>Chính sách đang áp dụng; ai cũng xem được để hiển thị ở trang thuê / checkout.</summary>
    [HttpGet("/api/v1/rental-policies/active")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RentalPolicyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveAsync(CancellationToken cancellationToken)
        => Ok(ApiResponse<RentalPolicyResponse>.Ok(
            await rentalPolicyService.GetActiveAsync(cancellationToken), "Lấy chính sách thuê đồ thành công"));

    /// <summary>Tạo version mới và kích hoạt ngay; đơn đã đặt vẫn giữ version cũ.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<RentalPolicyResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateVersionAsync(
        [FromBody] CreateRentalPolicyVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rentalPolicyService.CreateVersionAsync(currentUser.RequiredUserId, request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<RentalPolicyResponse>.Ok(result, "Đã tạo chính sách thuê đồ mới", StatusCodes.Status201Created));
    }
}
