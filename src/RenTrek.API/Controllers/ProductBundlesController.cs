using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.ProductBundle.Requests;
using RenTrek.BLL.DTOs.ProductBundle.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho product bundles (combo thiết bị).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/bundles")]
public class ProductBundlesController : ControllerBase
{
    private readonly IProductBundleService _productBundleService;

    public ProductBundlesController(IProductBundleService productBundleService)
    {
        _productBundleService = productBundleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ProductBundleResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAllAsync([FromQuery] ProductBundleQueryRequest request, CancellationToken cancellationToken)
    {
        var bundles = await _productBundleService.GetAllAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<ProductBundleResponse>>.Ok(bundles, "Lấy danh sách combo thành công"));
    }

    [HttpGet("{bundleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductBundleDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByIdAsync(Guid bundleId, [FromQuery] RentalPeriodQueryRequest request, CancellationToken cancellationToken)
    {
        var bundle = await _productBundleService.GetByIdAsync(bundleId, request, cancellationToken);
        return Ok(ApiResponse<ProductBundleDetailResponse>.Ok(bundle, "Lấy combo thành công"));
    }
}
