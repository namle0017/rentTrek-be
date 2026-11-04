using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Services.Admin;

namespace RenTrek.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/management/posts")]
[Authorize(Roles = "Admin,Staff")]
public sealed class AdminPostsController(IAdminService service) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetPostsAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PostWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SavePostAsync(null, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(result, "Tạo thành công", StatusCodes.Status201Created));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetPostAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PostWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SavePostAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deleted(Guid id, CancellationToken cancellationToken)
    {
        await service.SetPostStatusAsync(id, "Deleted", cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Published(Guid id, CancellationToken cancellationToken)
    {
        await service.SetPostStatusAsync(id, "Published", cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archived(Guid id, CancellationToken cancellationToken)
    {
        await service.SetPostStatusAsync(id, "Archived", cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành công"));
    }

    [HttpGet("{id:guid}/read-analytics")]
    public async Task<IActionResult> Analytics(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetReadAnalyticsAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, "Thành công"));
    }
}
