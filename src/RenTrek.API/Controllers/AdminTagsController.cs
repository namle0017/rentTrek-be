using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.BLL.Constants;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

[ApiController]
[Route("api/v1/admin/tags")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminTagsController(IAdminMasterDataService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AdminTagResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetTagsAsync(request, cancellationToken);
        return Ok(ApiResponse<List<AdminTagResponse>>.Ok(result, "Lấy danh sách thẻ thành công"));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateTagRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateTagAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(result, "Tạo thẻ thành công", StatusCodes.Status201Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateTagRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateTagAsync(id, request, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(result, "Cập nhật thẻ thành công"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteTagAsync(id, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xóa thẻ thành công"));
    }
}
