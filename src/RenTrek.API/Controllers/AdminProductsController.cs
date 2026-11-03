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
[Route("api/v1/admin/products")]
public sealed class AdminProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminProductListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AdminProductQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await productService.GetAdminProductsAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminProductListItemResponse>>.Ok(result, "Lấy danh sách sản phẩm vận hành thành công"));
    }

    /// <summary>Chi tiết sản phẩm cho vận hành (gồm cả sản phẩm / biến thể tạm ngưng).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await productService.GetAdminProductByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminProductDetailResponse>.Ok(result, "Lấy chi tiết sản phẩm thành công"));
    }
}
