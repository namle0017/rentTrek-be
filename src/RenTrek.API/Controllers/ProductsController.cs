using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Product.Requests;
using RenTrek.BLL.DTOs.Product.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho products.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ProductResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAllAsync([FromQuery] ProductQueryRequest request, CancellationToken cancellationToken)
    {
        var products = await _productService.GetAllAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<ProductResponse>>.Ok(products, "Lấy danh sách sản phẩm thành công"));
    }

    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductCategoryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await _productService.GetCategoriesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProductCategoryResponse>>.Ok(categories, "Lấy danh mục sản phẩm thành công"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ProductDetailResponse>.Ok(product, "Lấy sản phẩm thành công"));
    }

    [HttpGet("{id:guid}/related")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRelatedAsync(Guid id, [FromQuery] int limit = 4, CancellationToken cancellationToken = default)
    {
        var related = await _productService.GetRelatedAsync(id, limit, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProductResponse>>.Ok(related, "Lấy danh sách sản phẩm liên quan thành công"));
    }
}
