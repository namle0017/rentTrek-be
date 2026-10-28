using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.RentalAvailability;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP kiểm tra khả dụng thiết bị cho giỏ thuê.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/rental-availability")]
public class RentalAvailabilityController : ControllerBase
{
    private readonly IRentalAvailabilityService _rentalAvailabilityService;

    public RentalAvailabilityController(IRentalAvailabilityService rentalAvailabilityService)
    {
        _rentalAvailabilityService = rentalAvailabilityService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CheckRentalAvailabilityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckAsync([FromBody] CheckRentalAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var result = await _rentalAvailabilityService.CheckAsync(request, cancellationToken);
        return Ok(ApiResponse<CheckRentalAvailabilityResponse>.Ok(result, "Kiểm tra khả dụng thành công"));
    }
}
