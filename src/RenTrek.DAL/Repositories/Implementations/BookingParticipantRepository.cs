using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class BookingParticipantRepository : GenericRepository<BookingParticipant>, IBookingParticipantRepository
{
    public BookingParticipantRepository(AppDbContext context) : base(context)
    {
    }
}
