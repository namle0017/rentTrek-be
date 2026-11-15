using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho hướng dẫn viên xem các TripGroup đã được giao (gồm cả Draft và Published).
/// </summary>
[ApiController]
[Authorize(Roles = RoleNames.Guide)]
[Route("api/v1/guide/trip-groups")]
public sealed class GuideTripGroupsController(ITripGroupViewService tripGroupViewService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GuideTripGroupResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignedGroupsAsync(CancellationToken cancellationToken)
    {
        var result = await tripGroupViewService.GetGuideGroupsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GuideTripGroupResponse>>.Ok(result, "Lấy danh sách group được phân công thành công"));
    }

    [HttpGet("{groupId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GuideTripGroupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignedGroupByIdAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var result = await tripGroupViewService.GetGuideGroupByIdAsync(groupId, cancellationToken);
        return Ok(ApiResponse<GuideTripGroupResponse>.Ok(result, "Lấy chi tiết group được phân công thành công"));
    }
}
