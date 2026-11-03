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
[Authorize(Roles = $"{RoleNames.Guide},{RoleNames.TourGuide}")]
[Route("api/v1/guide/trip-groups")]
public sealed class GuideTripGroupsController(ITripGroupService tripGroupService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAssignedGroups(CancellationToken cancellationToken)
    {
        var result = await tripGroupService.GetGuideGroupsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GuideTripGroupResponse>>.Ok(result, "Lấy danh sách group được phân công thành công"));
    }

    [HttpGet("{groupId:guid}")]
    public async Task<IActionResult> GetAssignedGroupById(Guid groupId, CancellationToken cancellationToken)
    {
        var result = await tripGroupService.GetGuideGroupByIdAsync(groupId, cancellationToken);
        return Ok(ApiResponse<GuideTripGroupResponse>.Ok(result, "Lấy chi tiết group được phân công thành công"));
    }
}
