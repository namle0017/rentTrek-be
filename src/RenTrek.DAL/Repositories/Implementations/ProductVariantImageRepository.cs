using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class ProductVariantImageRepository : GenericRepository<ProductVariantImage>, IProductVariantImageRepository
{
    public ProductVariantImageRepository(AppDbContext context) : base(context)
    {
    }
}
