using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.DevClock.Requests;
using RenTrek.BLL.DTOs.DevClock.Responses;
using RenTrek.BLL.Helpers.Implementations;

namespace RenTrek.API.Controllers;

/// <summary>
/// Đồng hồ giả lập cho Development: tua "giờ hiện tại" của server để test luồng gần / trong / sau chuyến
/// và trả đồ trễ. Ngoài Development (SimulatedTimeProvider không được đăng ký) mọi endpoint trả 404.
/// Phiên đăng nhập và job xóa dữ liệu hoàn tiền luôn dùng giờ thật.
/// </summary>
[ApiController]
[Route("api/v1/dev/clock")]
[Produces("application/json")]
public sealed class DevClockController(IServiceProvider services) : ControllerBase
{
    private SimulatedTimeProvider? Clock => services.GetService<SimulatedTimeProvider>();

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<DevClockResponse>), StatusCodes.Status200OK)]
    public IActionResult Get()
        => Clock is { } clock ? Ok(ApiResponse<DevClockResponse>.Ok(Map(clock), "Lấy giờ hệ thống thành công")) : NotFound();

    [HttpPut]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<DevClockResponse>), StatusCodes.Status200OK)]
    public IActionResult Set([FromBody] SetDevClockRequest request)
    {
        if (Clock is not { } clock)
            return NotFound();

        clock.SetUtcNow(request.Now);
        return Ok(ApiResponse<DevClockResponse>.Ok(Map(clock), "Đã chuyển giờ hệ thống"));
    }

    [HttpDelete]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<DevClockResponse>), StatusCodes.Status200OK)]
    public IActionResult Reset()
    {
        if (Clock is not { } clock)
            return NotFound();

        clock.Reset();
        return Ok(ApiResponse<DevClockResponse>.Ok(Map(clock), "Đã về giờ thật"));
    }

    private static DevClockResponse Map(SimulatedTimeProvider clock)
        => new(clock.Offset != TimeSpan.Zero, clock.GetUtcNow(), clock.RealUtcNow, (long)clock.Offset.TotalSeconds);
}
