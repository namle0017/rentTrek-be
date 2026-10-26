using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.DTOs.Product;
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
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAllAsync([FromQuery] ProductQueryRequest request, CancellationToken cancellationToken)
    {
        var products = await _productService.GetAllAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<ProductResponse>>.Ok(products, "Lấy danh sách sản phẩm thành công"));
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
}
