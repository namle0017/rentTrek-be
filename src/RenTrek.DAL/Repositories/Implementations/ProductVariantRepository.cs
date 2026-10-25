using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class ProductVariantRepository : GenericRepository<ProductVariant>, IProductVariantRepository
{
    public ProductVariantRepository(AppDbContext context) : base(context)
    {
    }
}
