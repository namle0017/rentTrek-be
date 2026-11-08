using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Màn O34: chính sách hoàn tiền có phiên bản (03-API-CONTRACT.md §20).
/// </summary>
[ApiController]
[Route("api/v1/admin/refund-policies")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminRefundPoliciesController(IAdminRefundPolicyService service) : ControllerBase
{
    /// <summary>20.1. Danh sách chính sách hoàn tiền.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RefundPolicyListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] string? policyType, CancellationToken cancellationToken)
    {
        var result = await service.GetPoliciesAsync(policyType, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RefundPolicyListItemResponse>>.Ok(result, "Lấy danh sách chính sách hoàn tiền thành công"));
    }

    /// <summary>20.2. Tạo chính sách hoàn tiền mới.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveRefundPolicyRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreatePolicyAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Tạo chính sách hoàn tiền thành công", StatusCodes.Status201Created));
    }

    /// <summary>20.3. Chi tiết chính sách và bảng bậc hoàn tiền.</summary>
    [HttpGet("{policyId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RefundPolicyDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetailAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var result = await service.GetPolicyAsync(policyId, cancellationToken);
        return Ok(ApiResponse<RefundPolicyDetailResponse>.Ok(result, "Lấy chi tiết chính sách hoàn tiền thành công"));
    }

    /// <summary>20.4. Sửa chính sách (400 POLICY_ALREADY_IN_USE nếu đã có chuyến/đơn dùng).</summary>
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

    /// <summary>20.6. Nhân bản sang phiên bản mới; trả id bản mới.</summary>
    [HttpPost("{policyId:guid}/clone-version")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CloneVersionAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var result = await service.CloneVersionAsync(policyId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Tạo phiên bản chính sách mới thành công", StatusCodes.Status201Created));
    }
}
