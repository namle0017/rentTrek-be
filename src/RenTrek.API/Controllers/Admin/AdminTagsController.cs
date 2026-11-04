using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/tags")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminTagsController(IAdminService service) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetTagsAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] TagWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SaveTagAsync(null, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(result, "Tạo thành công", StatusCodes.Status201Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TagWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SaveTagAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteTagAsync(id, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }
}
