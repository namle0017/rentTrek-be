using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.BLL.Constants;
using RenTrek.BLL.Services.Implementations.Payments;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers.Payments;

[ApiController, Authorize(Roles = RoleNames.Customer), Route("api/v1/orders/{orderId:guid}/cancel")]
public sealed class OrderCancellationController(OrderPaymentLifecycleService service, ICurrentUserService user) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Cancel(Guid orderId, CancellationToken ct)
    {
        await service.CancelAsync(user.RequiredUserId, orderId, ct);
        return Ok(Common.ApiResponse<object?>.Ok(null, "Đơn đã hủy; hoàn tiền cần gửi yêu cầu theo policy."));
    }
}
