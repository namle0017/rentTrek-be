using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TourRecurrenceRuleRepository : GenericRepository<TourRecurrenceRule>, ITourRecurrenceRuleRepository
{
    public TourRecurrenceRuleRepository(AppDbContext context) : base(context)
    {
    }
}
