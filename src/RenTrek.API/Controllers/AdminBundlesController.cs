using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Màn O19: combo thiết bị, thành phần và bảng giá combo (03-API-CONTRACT.md §13.14–13.21).
/// </summary>
[ApiController]
[Route("api/v1/admin/bundles")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminBundlesController(IAdminProductCatalogService service) : ControllerBase
{
    /// <summary>13.14. Danh sách combo (search, status, categoryId, phân trang).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminBundleListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetBundlesAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminBundleListItemResponse>>.Ok(result, "Lấy danh sách combo thành công"));
    }

    /// <summary>13.15. Tạo combo (Draft / Inactive); trả id.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveBundleRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateBundleAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Tạo combo thành công", StatusCodes.Status201Created));
    }

    /// <summary>13.16. Chi tiết combo: thông tin, thành phần, bậc giá.</summary>
    [HttpGet("{bundleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminBundleDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetailAsync(Guid bundleId, CancellationToken cancellationToken)
    {
        var result = await service.GetBundleAsync(bundleId, cancellationToken);
        return Ok(ApiResponse<AdminBundleDetailResponse>.Ok(result, "Lấy chi tiết combo thành công"));
    }

    /// <summary>13.17. Sửa combo; chuyển Active cần ít nhất 1 thành phần.</summary>
    [HttpPut("{bundleId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid bundleId, [FromBody] SaveBundleRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateBundleAsync(bundleId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật combo thành công"));
    }

    /// <summary>13.18. Xoá mềm combo.</summary>
    [HttpDelete("{bundleId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync(Guid bundleId, CancellationToken cancellationToken)
    {
        await service.DeleteBundleAsync(bundleId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xoá combo thành công"));
    }

    /// <summary>13.19. Thay toàn bộ thành phần combo.</summary>
    [HttpPut("{bundleId:guid}/items")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetItemsAsync(Guid bundleId, [FromBody] BundleItemsRequest request, CancellationToken cancellationToken)
    {
        await service.SetBundleItemsAsync(bundleId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật thành phần combo thành công"));
    }

    /// <summary>13.20. Bảng bậc giá của combo.</summary>
    [HttpGet("{bundleId:guid}/price-tiers")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminPriceTierItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PriceTiersAsync(Guid bundleId, CancellationToken cancellationToken)
    {
        var result = await service.GetBundlePriceTiersAsync(bundleId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminPriceTierItemResponse>>.Ok(result, "Lấy bảng giá combo thành công"));
    }

    /// <summary>13.21. Thay toàn bộ bảng bậc giá của combo.</summary>
    [HttpPut("{bundleId:guid}/price-tiers")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPriceTiersAsync(Guid bundleId, [FromBody] PriceTiersRequest request, CancellationToken cancellationToken)
    {
        await service.SetBundlePriceTiersAsync(bundleId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật bảng giá combo thành công"));
    }
}
