using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class SoftSkillRepository : GenericRepository<SoftSkill>, ISoftSkillRepository
{
    public SoftSkillRepository(AppDbContext context) : base(context)
    {
    }
}
