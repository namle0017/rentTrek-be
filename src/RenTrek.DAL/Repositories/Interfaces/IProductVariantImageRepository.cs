using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IProductVariantImageRepository : IGenericRepository<ProductVariantImage>
{
    /// <summary>Ảnh chưa xoá của biến thể (tracking), theo thứ tự hiển thị.</summary>
    Task<List<ProductVariantImage>> GetByVariantAsync(Guid variantId, CancellationToken cancellationToken = default);
}
