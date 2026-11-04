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
[Route("api/v1/admin/soft-skills")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminSoftSkillsController(IAdminMasterDataService service) : ControllerBase
{

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AdminSoftSkillResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetSoftSkillsAsync(request, cancellationToken);
        return Ok(ApiResponse<List<AdminSoftSkillResponse>>.Ok(result, "Lấy danh sách kỹ năng mềm thành công"));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateSoftSkillRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateSoftSkillAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(result, "Tạo kỹ năng mềm thành công", StatusCodes.Status201Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateSoftSkillRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateSoftSkillAsync(id, request, cancellationToken);
        return Ok(ApiResponse<Guid>.Ok(result, "Cập nhật kỹ năng mềm thành công"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteSoftSkillAsync(id, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xóa kỹ năng mềm thành công"));
    }
}
