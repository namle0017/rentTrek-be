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
[Route("api/v1/admin/settings")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminSettingsController(IAdminGovernanceService service) : ControllerBase
{

    [HttpGet]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<List<AdminSettingResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var result = await service.GetSettingsAsync(cancellationToken);
        return Ok(ApiResponse<List<AdminSettingResponse>>.Ok(result, "Lấy danh sách cấu hình thành công"));
    }

    [HttpPut("{configKey}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(string configKey, [FromBody] UpdateSettingRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateSettingAsync(configKey, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật cấu hình thành công"));
    }
}
