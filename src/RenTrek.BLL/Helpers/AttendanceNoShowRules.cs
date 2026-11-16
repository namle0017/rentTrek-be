using RenTrek.BLL.Constants;
using RenTrek.BLL.Exceptions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Điểm danh hoạt động đầu tiên của chuyến quyết định NoShow: Absent ⇒ NoShow; Present/Late ⇒ trả về Pending.
/// Excused và các hoạt động sau không đổi trạng thái hành khách. NoShow không mở hồ sơ hoàn tiền.
/// Chưa hết thời gian chờ (no_show_grace_minutes của chính sách, tính từ giờ bắt đầu hoạt động đầu) thì không cho ghi Absent.
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

        if (statusByParticipant.Values.Any(s => s == AttendanceStatuses.Absent))
        {
            var policy = await unitOfWork.RefundPolicies.GetDetailAsync(trip.RefundPolicyId, cancellationToken);
            var deadline = FirstActivityStartUtc(trip, firstActivity).AddMinutes(policy?.NoShowGraceMinutes ?? 0);
            if (utcNow < deadline.UtcDateTime)
                throw new ConflictException(
                    $"Chưa hết {policy?.NoShowGraceMinutes} phút chờ khách (đến {deadline.ToOffset(VnOffset):HH:mm dd/MM}). Khách tới trong thời gian chờ ghi Late",
                    ErrorCodes.AttendanceNotEditable);
        }

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

    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    /// <summary>Giờ bắt đầu hoạt động đầu: ngày khởi hành (giờ VN) + (DayNumber - 1) ngày + StartTime.</summary>
    internal static DateTimeOffset FirstActivityStartUtc(Trip trip, TourActivity activity)
    {
        var startDay = trip.StartAt.ToOffset(VnOffset).Date;
        return new DateTimeOffset(startDay.AddDays(activity.DayNumber - 1) + activity.StartTime, VnOffset).ToUniversalTime();
    }
}
