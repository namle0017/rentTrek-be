using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Màn O34: chính sách hoàn tiền có phiên bản (03-API-CONTRACT.md §20). Staff/Admin xem; chỉ Admin ghi.
/// </summary>
[ApiController]
[Route("api/v1/admin/refund-policies")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminRefundPoliciesController(IAdminRefundPolicyService service) : ControllerBase
{
    /// <summary>20.1. Danh sách chính sách hoàn tiền (phân trang, lọc, sắp xếp).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RefundPolicyListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] RefundPolicyQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetPoliciesAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<RefundPolicyListItemResponse>>.Ok(result, "Lấy danh sách chính sách hoàn tiền thành công"));
    }

    /// <summary>20.2. Tạo chính sách hoàn tiền mới cùng bảng bậc (nháp hoặc áp dụng ngay).</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveRefundPolicyRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreatePolicyAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Tạo chính sách hoàn tiền thành công", StatusCodes.Status201Created));
    }

    /// <summary>20.3. Chi tiết chính sách, bảng bậc hoàn tiền, lịch sử phiên bản và cờ thao tác.</summary>
    [HttpGet("{policyId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RefundPolicyDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetailAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var result = await service.GetPolicyAsync(policyId, cancellationToken);
        return Ok(ApiResponse<RefundPolicyDetailResponse>.Ok(result, "Lấy chi tiết chính sách hoàn tiền thành công"));
    }

    /// <summary>20.4. Sửa chính sách và bảng bậc (409 POLICY_ALREADY_IN_USE nếu đã có chuyến/đơn dùng).</summary>
    [HttpPut("{policyId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid policyId, [FromBody] SaveRefundPolicyRequest request, CancellationToken cancellationToken)
    {
        await service.UpdatePolicyAsync(policyId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật chính sách hoàn tiền thành công"));
    }

    /// <summary>20.5. Thay toàn bộ bảng bậc hoàn tiền.</summary>
    [HttpPut("{policyId:guid}/rules")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RulesAsync(Guid policyId, [FromBody] RefundPolicyRulesRequest request, CancellationToken cancellationToken)
    {
        await service.SetRulesAsync(policyId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật bảng quy tắc hoàn tiền thành công"));
    }

    /// <summary>20.6. Nhân bản thành bản nháp phiên bản mới; trả id bản mới.</summary>
    [HttpPost("{policyId:guid}/clone-version")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CloneVersionAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var result = await service.CloneVersionAsync(policyId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Tạo phiên bản chính sách mới thành công", StatusCodes.Status201Created));
    }

    /// <summary>20.7. Kích hoạt bản nháp (hoặc bản Archived mới nhất) và đóng các bản cũ.</summary>
    [HttpPost("{policyId:guid}/activate")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateAsync(Guid policyId, CancellationToken cancellationToken)
    {
        await service.ActivateAsync(policyId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Áp dụng chính sách hoàn tiền thành công"));
    }

    /// <summary>20.8. Ngừng áp dụng chính sách (409 REFUND_POLICY_LAST_ACTIVE nếu là bản hiệu lực duy nhất của loại).</summary>
    [HttpPost("{policyId:guid}/archive")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ArchiveAsync(Guid policyId, CancellationToken cancellationToken)
    {
        await service.ArchiveAsync(policyId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Ngừng áp dụng chính sách hoàn tiền thành công"));
    }

    /// <summary>20.9. Xoá mềm chính sách chưa được dùng.</summary>
    [HttpDelete("{policyId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync(Guid policyId, CancellationToken cancellationToken)
    {
        await service.DeletePolicyAsync(policyId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xoá chính sách hoàn tiền thành công"));
    }
}
