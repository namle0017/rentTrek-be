using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/tours")]
public sealed class AdminToursController(ITourService tourService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminTourListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AdminTourQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await tourService.GetAdminToursAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminTourListItemResponse>>.Ok(result, "Lấy danh sách tour vận hành thành công"));
    }
}
