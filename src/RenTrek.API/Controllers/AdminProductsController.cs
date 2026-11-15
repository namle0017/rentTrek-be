using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Exceptions;

namespace RenTrek.API.Controllers;

[ApiController]
// Staff trực tiếp quản lý kho nên được tạo / sửa sản phẩm, biến thể, ảnh và bảng giá; chỉ thao tác xoá giữ riêng Admin.
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/products")]
public sealed class AdminProductsController(IProductService productService, IAdminProductCatalogService catalogService,
    IProductImageUploadService imageUpload)
    : ControllerBase
{
    /// <summary>Upload ảnh sản phẩm lên Cloudinary, sau đó gắn URL vào biến thể.</summary>
    [HttpPost("images/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(IProductImageUploadService.MaxBytes + 64 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<IActionResult> UploadImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("Vui lòng chọn ảnh sản phẩm");
        await using var stream = file.OpenReadStream();
        var url = await imageUpload.UploadAsync(stream, file.FileName, file.ContentType, file.Length, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { imageUrl = url }, "Tải ảnh lên thành công", StatusCodes.Status201Created));
    }

    /// <summary>13.3. Tạo sản phẩm cha; trả id.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveProductRequest request, CancellationToken cancellationToken)
    {
        var result = await catalogService.CreateProductAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Tạo sản phẩm thành công", StatusCodes.Status201Created));
    }

    /// <summary>13.4. Sửa sản phẩm cha.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync([FromRoute] Guid id, [FromBody] SaveProductRequest request, CancellationToken cancellationToken)
    {
        await catalogService.UpdateProductAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật sản phẩm thành công"));
    }

    /// <summary>13.5. Xoá mềm sản phẩm.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await catalogService.DeleteProductAsync(id, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xoá sản phẩm thành công"));
    }

    /// <summary>13.6. Thêm biến thể cho sản phẩm (409 SKU_ALREADY_EXISTS nếu trùng); trả id.</summary>
    [HttpPost("{id:guid}/variants")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateVariantAsync([FromRoute] Guid id, [FromBody] SaveVariantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.CreateVariantAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Tạo biến thể thành công", StatusCodes.Status201Created));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminProductListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync([FromQuery] AdminProductQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await productService.GetAdminProductsAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminProductListItemResponse>>.Ok(result, "Lấy danh sách sản phẩm vận hành thành công"));
    }

    /// <summary>Chi tiết sản phẩm cho vận hành (gồm cả sản phẩm / biến thể tạm ngưng).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await productService.GetAdminProductByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminProductDetailResponse>.Ok(result, "Lấy chi tiết sản phẩm thành công"));
    }
}
