using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class EquipmentAssetRepository : GenericRepository<EquipmentAsset>, IEquipmentAssetRepository
{
    public EquipmentAssetRepository(AppDbContext context) : base(context)
    {
    }
}
