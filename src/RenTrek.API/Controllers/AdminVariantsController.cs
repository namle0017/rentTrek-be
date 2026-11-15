using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Màn O17 / O18: biến thể, ảnh biến thể, bảng bậc giá biến thể và bảng giá tổng hợp (03-API-CONTRACT.md §13.7–13.13).
/// </summary>
[ApiController]
[Route("api/v1/admin/variants")]
// Staff trực tiếp quản lý kho nên được tạo / sửa sản phẩm, biến thể, ảnh và bảng giá; chỉ thao tác xoá giữ riêng Admin.
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public sealed class AdminVariantsController(IAdminProductCatalogService service) : ControllerBase
{
    /// <summary>13.7. Sửa biến thể (409 SKU_ALREADY_EXISTS nếu trùng SKU).</summary>
    [HttpPut("{variantId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid variantId, [FromBody] SaveVariantRequest request, CancellationToken cancellationToken)
    {
        await service.UpdateVariantAsync(variantId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật biến thể thành công"));
    }

    /// <summary>13.8. Xoá mềm biến thể.</summary>
    [HttpDelete("{variantId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync(Guid variantId, CancellationToken cancellationToken)
    {
        await service.DeleteVariantAsync(variantId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xoá biến thể thành công"));
    }

    /// <summary>13.9. Thêm ảnh biến thể; trả id ảnh.</summary>
    [HttpPost("{variantId:guid}/images")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddImageAsync(Guid variantId, [FromBody] SaveImageRequest request, CancellationToken cancellationToken)
    {
        var result = await service.AddVariantImageAsync(variantId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Thêm ảnh biến thể thành công", StatusCodes.Status201Created));
    }

    /// <summary>13.10. Xoá mềm ảnh biến thể.</summary>
    [HttpDelete("{variantId:guid}/images/{imageId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteImageAsync(Guid variantId, Guid imageId, CancellationToken cancellationToken)
    {
        await service.DeleteVariantImageAsync(variantId, imageId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xoá ảnh biến thể thành công"));
    }

    /// <summary>Đặt một ảnh đã có của biến thể làm ảnh đại diện.</summary>
    [HttpPatch("{variantId:guid}/images/{imageId:guid}/primary")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPrimaryImageAsync(Guid variantId, Guid imageId, CancellationToken cancellationToken)
    {
        await service.SetPrimaryVariantImageAsync(variantId, imageId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đặt ảnh đại diện biến thể thành công"));
    }

    /// <summary>13.11. Bảng bậc giá thuê theo ngày của biến thể.</summary>
    [HttpGet("{variantId:guid}/price-tiers")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminPriceTierItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PriceTiersAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var result = await service.GetVariantPriceTiersAsync(variantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminPriceTierItemResponse>>.Ok(result, "Lấy bảng giá biến thể thành công"));
    }

    /// <summary>13.12. Thay toàn bộ bảng bậc giá của biến thể.</summary>
    [HttpPut("{variantId:guid}/price-tiers")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPriceTiersAsync(Guid variantId, [FromBody] PriceTiersRequest request, CancellationToken cancellationToken)
    {
        await service.SetVariantPriceTiersAsync(variantId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật bảng giá biến thể thành công"));
    }

    /// <summary>13.13. Bảng giá tổng hợp mọi biến thể và combo, lọc theo danh mục.</summary>
    [HttpGet("~/api/v1/admin/pricing")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminPricingItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PricingAsync([FromQuery] Guid? categoryId, CancellationToken cancellationToken)
    {
        var result = await service.GetPricingAsync(categoryId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminPricingItemResponse>>.Ok(result, "Lấy bảng giá tổng hợp thành công"));
    }
}
