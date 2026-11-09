using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Định nghĩa dịch vụ chia nhóm chuyến đi dạng Kanban và phân công Hướng dẫn viên (Màn O12).
/// </summary>
public interface IAdminTripGroupService
{
    Task<KanbanGroupsBoardResponse> GetGroupsBoardAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<KanbanGroupColumnResponse> CreateGroupAsync(
        Guid tripId,
        CreateTripGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<KanbanGroupColumnResponse> UpdateGroupAsync(
        Guid tripId,
        Guid groupId,
        UpdateTripGroupRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteGroupAsync(
        Guid tripId,
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task<KanbanGroupsBoardResponse> AssignParticipantGroupAsync(
        Guid tripId,
        AssignParticipantGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<AutoGroupResultResponse> AutoGroupAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<SplitApproveBookingResponse> ApproveBookingSplitAsync(
        Guid tripId,
        Guid bookingId,
        SplitApproveBookingRequest request,
        CancellationToken cancellationToken = default);
}
