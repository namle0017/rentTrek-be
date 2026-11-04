using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/settings")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminSettingsController(IAdminService service) : ControllerBase
{

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await service.GetSettingsAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPut("{configKey}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string configKey, [FromBody] SettingWriteRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateSettingAsync(configKey, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }
}
