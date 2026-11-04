using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/locations")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminLocationsController(IAdminService service) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetLocationsAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] LocationWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SaveLocationAsync(null, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(result, "Tạo thành công", StatusCodes.Status201Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] LocationWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SaveLocationAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteLocationAsync(id, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }
}
