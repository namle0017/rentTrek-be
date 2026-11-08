using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.BLL.Constants;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

[ApiController]
[Route("api/v1/admin/locations")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminLocationsController(IAdminMasterDataService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AdminLocationResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetLocationsAsync(request, cancellationToken);
        return Ok(ApiResponse<List<AdminLocationResponse>>.Ok(result, "Lấy danh sách địa điểm thành công"));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateLocationRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateLocationAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(result, "Tạo địa điểm thành công", StatusCodes.Status201Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateLocationRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateLocationAsync(id, request, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(result, "Cập nhật địa điểm thành công"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteLocationAsync(id, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xóa địa điểm thành công"));
    }
}
