using RenTrek.BLL.Constants;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Điểm danh hoạt động đầu tiên của chuyến quyết định NoShow: Absent ⇒ NoShow; Present/Late ⇒ trả về Pending.
/// Excused và các hoạt động sau không đổi trạng thái hành khách. NoShow không mở hồ sơ hoàn tiền.
/// </summary>
internal static class AttendanceNoShowRules
{
    public static async Task ApplyAsync(
        IUnitOfWork unitOfWork,
        Trip trip,
        Guid activityId,
        IReadOnlyDictionary<Guid, string> statusByParticipant,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var firstActivity = (trip.Tour?.Activities ?? [])
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.DayNumber)
            .ThenBy(a => a.DisplayOrder)
            .ThenBy(a => a.StartTime)
            .FirstOrDefault();
        if (firstActivity?.Id != activityId || statusByParticipant.Count == 0)
            return;

        var participants = await unitOfWork.TripGroups.GetActiveParticipantsForUpdateByTripAsync(trip.TripId, cancellationToken);
        foreach (var participant in participants)
        {
            if (!statusByParticipant.TryGetValue(participant.ParticipantId, out var status)
                || participant.Status is BookingParticipantStatuses.Cancelled or BookingParticipantStatuses.Completed)
                continue;

            var target = status switch
            {
                AttendanceStatuses.Absent => BookingParticipantStatuses.NoShow,
                AttendanceStatuses.Present or AttendanceStatuses.Late when participant.Status == BookingParticipantStatuses.NoShow
                    => BookingParticipantStatuses.Pending,
                _ => participant.Status
            };
            if (target == participant.Status)
                continue;

            participant.Status = target;
            participant.UpdatedAt = utcNow;
        }
    }
}
