using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminAuditLogsController(IAdminService service) : ControllerBase
{

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> List([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetAuditLogsAsync(request, null, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }
}
