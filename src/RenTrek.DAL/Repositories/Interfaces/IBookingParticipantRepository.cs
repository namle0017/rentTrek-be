using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>
/// Định nghĩa truy cập dữ liệu cho hành khách tham gia chuyến đi (BookingParticipant).
/// </summary>
public interface IBookingParticipantRepository : IGenericRepository<BookingParticipant>
{
    Task<BookingParticipant?> GetParticipantForGuideAsync(
        Guid participantId,
        CancellationToken cancellationToken = default);
}
