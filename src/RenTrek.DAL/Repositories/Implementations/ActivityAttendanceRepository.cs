using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class ActivityAttendanceRepository : GenericRepository<ActivityAttendance>, IActivityAttendanceRepository
{
    public ActivityAttendanceRepository(AppDbContext context) : base(context)
    {
    }
}
