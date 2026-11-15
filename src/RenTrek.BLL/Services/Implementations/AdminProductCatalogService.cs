using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Entities.Enums;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Ghi danh mục cho thuê. Mọi thao tác ghi chạy trong transaction có khóa "catalog" và ghi audit log.
/// </summary>
public sealed class AdminProductCatalogService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminProductCatalogService
{
    // ponytail: một khóa cho cả danh mục, tách theo sản phẩm/combo nếu admin ghi đồng thời nhiều
    private const string LockKey = "catalog";
    private const string ActiveStatus = "Active";
    private const string InactiveStatus = "Inactive";

    public async Task<Guid> CreateProductAsync(SaveProductRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var productId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            await EnsureCategoryAsync(request.CategoryId, cancellationToken);
            await UnitOfWork.Products.AddAsync(new Product
            {
                Id = productId,
                CategoryId = request.CategoryId,
                CreatedByUserId = ActorId,
                Name = request.Name.Trim(),
                Description = Trim(request.Description),
                Status = ToCatalogStatus(request.Status),
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await AuditAsync("Create", "Product", productId, request, cancellationToken);
        }, cancellationToken);
        return productId;
    }

    public async Task UpdateProductAsync(Guid productId, SaveProductRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var product = await GetProductAsync(productId, cancellationToken);
            if (product.CategoryId != request.CategoryId) await EnsureCategoryAsync(request.CategoryId, cancellationToken);
            var before = new { product.Name, product.CategoryId, product.Description, Status = product.Status.ToString() };
            product.Name = request.Name.Trim();
            product.CategoryId = request.CategoryId;
            product.Description = Trim(request.Description);
            product.Status = ToCatalogStatus(request.Status);
            product.UpdatedAt = Now;
            await AuditAsync("Update", "Product", productId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var product = await GetProductAsync(productId, cancellationToken);
            product.IsDeleted = true;
            product.UpdatedAt = Now;
            await AuditAsync("Delete", "Product", productId, new { product.Name }, cancellationToken);
        }, cancellationToken);
    }

    private static decimal? ValidReplacementValue(decimal? value)
    {
        if (value is null)
            return null;
        if (value < 0 || value > 10_000_000_000m)
            throw new RenTrek.BLL.Exceptions.ValidationException("replacementValue", "Giá trị thiết bị phải từ 0 đến 10 tỷ đồng");
        return Math.Round(value.Value, 0);
    }

    public async Task<Guid> CreateVariantAsync(Guid productId, SaveVariantRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var variantId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            await GetProductAsync(productId, cancellationToken);
            var sku = await EnsureSkuFreeAsync(request.Sku, null, cancellationToken);
            await UnitOfWork.ProductVariants.AddAsync(new ProductVariant
            {
                Id = variantId,
                ProductId = productId,
                Sku = sku,
                Name = request.VariantName.Trim(),
                Size = Trim(request.Size),
                Color = Trim(request.Color),
                ReplacementValue = ValidReplacementValue(request.ReplacementValue),
                Status = ToCatalogStatus(request.Status),
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await AuditAsync("Create", "ProductVariant", variantId, new { productId, request }, cancellationToken);
        }, cancellationToken);
        return variantId;
    }

    public async Task UpdateVariantAsync(Guid variantId, SaveVariantRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var variant = await GetVariantAsync(variantId, cancellationToken);
            var sku = await EnsureSkuFreeAsync(request.Sku, variantId, cancellationToken);
            var before = new { variant.Sku, variant.Name, variant.Size, variant.Color, Status = variant.Status.ToString() };
            variant.Sku = sku;
            variant.Name = request.VariantName.Trim();
            variant.Size = Trim(request.Size);
            variant.Color = Trim(request.Color);
            variant.ReplacementValue = ValidReplacementValue(request.ReplacementValue);
            variant.Status = ToCatalogStatus(request.Status);
            variant.UpdatedAt = Now;
            await AuditAsync("Update", "ProductVariant", variantId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteVariantAsync(Guid variantId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var variant = await GetVariantAsync(variantId, cancellationToken);
            variant.IsDeleted = true;
            variant.UpdatedAt = Now;
            await AuditAsync("Delete", "ProductVariant", variantId, new { variant.Sku }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminPriceTierItemResponse>> GetVariantPriceTiersAsync(Guid variantId,
        CancellationToken cancellationToken)
    {
        _ = await UnitOfWork.ProductVariants.GetByIdAsync(variantId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy biến thể", ErrorCodes.NotFound);
        return ToTierResponses(await UnitOfWork.RentalPriceTiers.GetForVariantsAsync([variantId], cancellationToken));
    }

    public async Task SetVariantPriceTiersAsync(Guid variantId, PriceTiersRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var variant = await GetVariantAsync(variantId, cancellationToken);
            await ReplaceTiersAsync(variantId, null, request, cancellationToken);
            variant.UpdatedAt = Now;
            await AuditAsync("UpdatePriceTiers", "ProductVariant", variantId, request, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Guid> AddVariantImageAsync(Guid variantId, SaveImageRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var imageId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            var variant = await GetVariantAsync(variantId, cancellationToken);
            var images = await UnitOfWork.ProductVariantImages.GetByVariantAsync(variantId, cancellationToken);
            // Ảnh đầu tiên luôn là ảnh đại diện; chỉ giữ một ảnh đại diện mỗi biến thể
            var isPrimary = request.IsPrimary || images.Count == 0;
            if (isPrimary)
                foreach (var image in images.Where(image => image.IsPrimary))
                {
                    image.IsPrimary = false;
                    image.UpdatedAt = Now;
                }
            await UnitOfWork.ProductVariantImages.AddAsync(new ProductVariantImage
            {
                Id = imageId,
                ProductVariantId = variantId,
                ImageUrl = request.ImageUrl.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsPrimary = isPrimary,
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            variant.UpdatedAt = Now;
            await AuditAsync("AddImage", "ProductVariant", variantId, new { imageId, request }, cancellationToken);
        }, cancellationToken);
        return imageId;
    }

    public async Task DeleteVariantImageAsync(Guid variantId, Guid imageId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var variant = await GetVariantAsync(variantId, cancellationToken);
            var images = await UnitOfWork.ProductVariantImages.GetByVariantAsync(variantId, cancellationToken);
            var image = images.FirstOrDefault(x => x.Id == imageId)
                ?? throw new NotFoundException("Không tìm thấy ảnh", ErrorCodes.NotFound);
            image.IsDeleted = true;
            image.UpdatedAt = Now;
            if (image.IsPrimary)
            {
                image.IsPrimary = false;
                // Xoá ảnh đại diện thì ảnh còn lại đứng đầu lên thay
                var next = images.FirstOrDefault(x => x.Id != imageId);
                if (next is not null)
                {
                    next.IsPrimary = true;
                    next.UpdatedAt = Now;
                }
            }
            variant.UpdatedAt = Now;
            await AuditAsync("DeleteImage", "ProductVariant", variantId, new { imageId, image.ImageUrl }, cancellationToken);
        }, cancellationToken);
    }

    public async Task SetPrimaryVariantImageAsync(Guid variantId, Guid imageId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var variant = await GetVariantAsync(variantId, cancellationToken);
            var images = await UnitOfWork.ProductVariantImages.GetByVariantAsync(variantId, cancellationToken);
            var target = images.FirstOrDefault(x => x.Id == imageId)
                ?? throw new NotFoundException("Không tìm thấy ảnh", ErrorCodes.NotFound);
            foreach (var image in images)
            {
                var shouldBePrimary = image.Id == imageId;
                if (image.IsPrimary != shouldBePrimary)
                {
                    image.IsPrimary = shouldBePrimary;
                    image.UpdatedAt = Now;
                }
            }
            variant.UpdatedAt = Now;
            await AuditAsync("SetPrimaryImage", "ProductVariant", variantId, new { imageId, target.ImageUrl }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminPricingItemResponse>> GetPricingAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        var rows = await UnitOfWork.RentalPriceTiers.GetPricingOverviewAsync(categoryId, cancellationToken);
        return rows
            .GroupBy(x => (x.VariantId, x.BundleId))
            .Select(g =>
            {
                var first = g.First();
                return new AdminPricingItemResponse
                {
                    ItemType = first.VariantId.HasValue ? "Variant" : "Bundle",
                    ItemId = first.VariantId ?? first.BundleId!.Value,
                    Sku = first.Sku,
                    Name = first.Name,
                    CategoryId = first.CategoryId,
                    CategoryName = first.CategoryName,
                    Status = first.Status,
                    Tiers = g.Select(t => new AdminPriceTierItemResponse
                    {
                        PriceTierId = t.Id,
                        MinDays = t.MinDays,
                        MaxDays = t.MaxDays,
                        DailyPrice = t.DailyPrice
                    }).ToList()
                };
            })
            .ToList();
    }

    public async Task<PagedResponse<AdminBundleListItemResponse>> GetBundlesAsync(AdminQueryRequest request,
        CancellationToken cancellationToken)
    {
        CheckPaging(request);
        var page = await UnitOfWork.ProductBundles.GetAdminPagedAsync(request.Search, request.Status, request.CategoryId,
            request.PageNumber, request.PageSize, cancellationToken);
        return ToPaged(page, request, (AdminBundleRow x) => new AdminBundleListItemResponse
        {
            BundleId = x.BundleId,
            Name = x.Name,
            CategoryId = x.CategoryId,
            CategoryName = x.CategoryName,
            Status = x.Status,
            ItemsCount = x.ItemsCount,
            CreatedAt = x.CreatedAt
        });
    }

    public async Task<AdminBundleDetailResponse> GetBundleAsync(Guid bundleId, CancellationToken cancellationToken)
    {
        var bundle = await GetBundleEntityAsync(bundleId, cancellationToken);
        var category = await UnitOfWork.Categories.GetByIdAsync(bundle.CategoryId, cancellationToken);
        var items = await UnitOfWork.ProductBundleItems.GetByBundleForUpdateAsync(bundleId, cancellationToken);
        var variants = (await UnitOfWork.ProductVariants.GetActiveForRentalAsync(
                items.Select(i => i.ProductVariantId).ToList(), cancellationToken))
            .ToDictionary(v => v.VariantId);
        var tiers = await UnitOfWork.RentalPriceTiers.GetForBundlesAsync([bundleId], cancellationToken);

        return new AdminBundleDetailResponse
        {
            BundleId = bundle.BundleId,
            Name = bundle.Name,
            CategoryId = bundle.CategoryId,
            CategoryName = category?.Name ?? string.Empty,
            Description = bundle.Description,
            Status = bundle.Status,
            ReplacementValue = bundle.ReplacementValue,
            CreatedAt = bundle.CreatedAt,
            UpdatedAt = bundle.UpdatedAt,
            Items = items.Select(i =>
            {
                variants.TryGetValue(i.ProductVariantId, out var v);
                return new AdminBundleItemResponse
                {
                    BundleItemId = i.BundleItemId,
                    ProductVariantId = i.ProductVariantId,
                    Sku = v?.Sku ?? string.Empty,
                    ProductName = v?.ProductName ?? string.Empty,
                    VariantName = v?.VariantName ?? string.Empty,
                    Quantity = i.Quantity,
                    IsActive = v?.IsActive ?? false
                };
            }).ToList(),
            PriceTiers = ToTierResponses(tiers).ToList()
        };
    }

    public async Task<Guid> CreateBundleAsync(SaveBundleRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var status = ToBundleStatus(request.Status);
        Check(status != ActiveStatus, "status", "Combo mới chưa có thành phần; tạo ở trạng thái Draft rồi thêm thành phần trước khi bán");
        var bundleId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            await EnsureCategoryAsync(request.CategoryId, cancellationToken);
            await UnitOfWork.ProductBundles.AddAsync(new ProductBundle
            {
                BundleId = bundleId,
                CategoryId = request.CategoryId,
                Name = request.Name.Trim(),
                Description = Trim(request.Description),
                ReplacementValue = ValidReplacementValue(request.ReplacementValue),
                Status = status,
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await AuditAsync("Create", "ProductBundle", bundleId, request, cancellationToken);
        }, cancellationToken);
        return bundleId;
    }

    public async Task UpdateBundleAsync(Guid bundleId, SaveBundleRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var bundle = await GetBundleEntityAsync(bundleId, cancellationToken);
            if (bundle.CategoryId != request.CategoryId) await EnsureCategoryAsync(request.CategoryId, cancellationToken);
            var status = ToBundleStatus(request.Status);
            if (status == ActiveStatus)
                Check((await UnitOfWork.ProductBundleItems.GetByBundleForUpdateAsync(bundleId, cancellationToken)).Count > 0,
                    "status", "Combo cần ít nhất 1 thành phần trước khi bán");
            var before = new { bundle.Name, bundle.CategoryId, bundle.Description, bundle.Status };
            bundle.Name = request.Name.Trim();
            bundle.CategoryId = request.CategoryId;
            bundle.Description = Trim(request.Description);
            bundle.ReplacementValue = ValidReplacementValue(request.ReplacementValue);
            bundle.Status = status;
            bundle.UpdatedAt = Now;
            await AuditAsync("Update", "ProductBundle", bundleId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteBundleAsync(Guid bundleId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var bundle = await GetBundleEntityAsync(bundleId, cancellationToken);
            bundle.IsDeleted = true;
            bundle.UpdatedAt = Now;
            await AuditAsync("Delete", "ProductBundle", bundleId, new { bundle.Name }, cancellationToken);
        }, cancellationToken);
    }

    public async Task SetBundleItemsAsync(Guid bundleId, BundleItemsRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var bundle = await GetBundleEntityAsync(bundleId, cancellationToken);
            var ids = request.Items.Select(i => i.ProductVariantId).ToList();
            var active = (await UnitOfWork.ProductVariants.GetActiveForRentalAsync(ids, cancellationToken))
                .Where(v => v.IsActive).Select(v => v.VariantId).ToHashSet();
            var missing = ids.Where(id => !active.Contains(id)).ToList();
            Check(missing.Count == 0, "items", $"Biến thể không tồn tại hoặc ngừng bán: {string.Join(", ", missing)}");

            foreach (var old in await UnitOfWork.ProductBundleItems.GetByBundleForUpdateAsync(bundleId, cancellationToken))
            {
                old.IsDeleted = true;
                old.UpdatedAt = Now;
            }
            await UnitOfWork.ProductBundleItems.AddRangeAsync(request.Items.Select(i => new ProductBundleItem
            {
                BundleItemId = Guid.NewGuid(),
                BundleId = bundleId,
                ProductVariantId = i.ProductVariantId,
                Quantity = i.Quantity,
                CreatedAt = Now,
                UpdatedAt = Now
            }), cancellationToken);
            bundle.UpdatedAt = Now;
            await AuditAsync("UpdateItems", "ProductBundle", bundleId, request, cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminPriceTierItemResponse>> GetBundlePriceTiersAsync(Guid bundleId,
        CancellationToken cancellationToken)
    {
        await GetBundleEntityAsync(bundleId, cancellationToken);
        return ToTierResponses(await UnitOfWork.RentalPriceTiers.GetForBundlesAsync([bundleId], cancellationToken));
    }

    public async Task SetBundlePriceTiersAsync(Guid bundleId, PriceTiersRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var bundle = await GetBundleEntityAsync(bundleId, cancellationToken);
            await ReplaceTiersAsync(null, bundleId, request, cancellationToken);
            bundle.UpdatedAt = Now;
            await AuditAsync("UpdatePriceTiers", "ProductBundle", bundleId, request, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>Sản phẩm chưa xoá (tracking); 404 nếu không có.</summary>
    private async Task<Product> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await UnitOfWork.Products.GetByIdAsync(productId, cancellationToken);
        return product is { IsDeleted: false }
            ? product
            : throw new NotFoundException("Không tìm thấy sản phẩm", ErrorCodes.NotFound);
    }

    /// <summary>Biến thể chưa xoá, sản phẩm cha chưa xoá (tracking); 404 nếu không có.</summary>
    private async Task<ProductVariant> GetVariantAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var variant = await UnitOfWork.ProductVariants.GetByIdAsync(variantId, cancellationToken);
        return variant is { IsDeleted: false }
            ? variant
            : throw new NotFoundException("Không tìm thấy biến thể", ErrorCodes.NotFound);
    }

    /// <summary>Combo chưa xoá (tracking); 404 nếu không có.</summary>
    private async Task<ProductBundle> GetBundleEntityAsync(Guid bundleId, CancellationToken cancellationToken)
    {
        var bundle = await UnitOfWork.ProductBundles.GetByIdAsync(bundleId, cancellationToken);
        return bundle is { IsDeleted: false }
            ? bundle
            : throw new NotFoundException("Không tìm thấy combo", ErrorCodes.NotFound);
    }

    /// <summary>Danh mục phải tồn tại, chưa xoá và không phải danh mục tour.</summary>
    private async Task EnsureCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await UnitOfWork.Categories.GetByIdAsync(categoryId, cancellationToken);
        Check(category is { IsDeleted: false } && category.CategoryType != "TOUR", "categoryId",
            "Danh mục không tồn tại hoặc không phải danh mục sản phẩm");
    }

    /// <summary>Chuẩn hoá SKU (trim, in hoa) và chặn trùng; 409 SKU_ALREADY_EXISTS.</summary>
    private async Task<string> EnsureSkuFreeAsync(string sku, Guid? excludeVariantId, CancellationToken cancellationToken)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        if (await UnitOfWork.ProductVariants.SkuExistsAsync(normalized, excludeVariantId, cancellationToken))
            throw new ConflictException($"SKU '{normalized}' đã tồn tại", ErrorCodes.SkuAlreadyExists);
        return normalized;
    }

    /// <summary>Xoá mềm bậc giá cũ của biến thể / combo rồi thêm bậc mới theo thứ tự MinDays.</summary>
    private async Task ReplaceTiersAsync(Guid? variantId, Guid? bundleId, PriceTiersRequest request,
        CancellationToken cancellationToken)
    {
        foreach (var old in await UnitOfWork.RentalPriceTiers.GetForUpdateAsync(variantId, bundleId, cancellationToken))
        {
            old.IsDeleted = true;
            old.UpdatedAt = Now;
        }
        await UnitOfWork.RentalPriceTiers.AddRangeAsync(request.Tiers.OrderBy(t => t.MinDays).Select(t => new RentalPriceTier
        {
            Id = Guid.NewGuid(),
            ProductVariantId = variantId,
            BundleId = bundleId,
            MinDays = t.MinDays,
            MaxDays = t.MaxDays,
            DailyPrice = t.DailyPrice,
            CreatedAt = Now,
            UpdatedAt = Now
        }), cancellationToken);
    }

    private static List<AdminPriceTierItemResponse> ToTierResponses(IEnumerable<RentalPriceTierRow> rows)
        => rows.Select(t => new AdminPriceTierItemResponse
        {
            PriceTierId = t.Id,
            MinDays = t.MinDays,
            MaxDays = t.MaxDays,
            DailyPrice = t.DailyPrice
        }).ToList();

    /// <summary>Active giữ nguyên; Inactive / Draft lưu là Inactive (enum chỉ có 2 giá trị).</summary>
    private static CatalogStatus ToCatalogStatus(string status)
        => status == ActiveStatus ? CatalogStatus.Active : CatalogStatus.Inactive;

    private static string ToBundleStatus(string status) => status == ActiveStatus ? ActiveStatus : InactiveStatus;

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
