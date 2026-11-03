using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Nghiệp vụ quản trị Trip &amp; lịch lặp, tự xếp group sau thanh toán, điều phối group của Staff, màn hình Guide và màn hình Khách.
/// </summary>
public interface ITripGroupService
{
    // 7.1 Admin: cấu hình Tour, lịch lặp và Trip
    Task<TourGroupSettingsResponse> UpdateTourGroupSettingsAsync(
        Guid tourId,
        UpdateTourGroupSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<TourRecurrenceRuleResponse> CreateRecurrenceRuleAsync(
        Guid tourId,
        CreateTourRecurrenceRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TourRecurrenceRuleResponse>> GetRecurrenceRulesAsync(
        Guid tourId,
        CancellationToken cancellationToken = default);

    Task<GenerateRecurringTripsResponse> GenerateRecurringTripsAsync(
        GenerateRecurringTripsRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminTripResponse> GetAdminTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<AdminTripResponse> CreateTripAsync(
        CreateTripRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminTripResponse> UpdateTripAsync(
        Guid tripId,
        UpdateTripRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminTripResponse> UpdateTripStatusAsync(
        Guid tripId,
        UpdateTripStatusRequest request,
        CancellationToken cancellationToken = default);

    // 7.2 Tự xếp idempotent trong cùng transaction xác nhận thanh toán
    Task AutoAssignPaidBookingInTransactionAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    // 7.3 & 7.4 Snapshot chia đoàn (Staff/Admin hoặc Guide theo quyền), gán guide, chuyển, tách, tính lại đồ cấp và chốt Published
    Task<TripGroupingSnapshotResponse> GetGroupingSnapshotAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<TripGroupingSnapshotResponse> AssignGuideAsync(
        Guid tripId,
        Guid groupId,
        AssignTripGroupGuideRequest request,
        CancellationToken cancellationToken = default);

    Task<TripGroupingSnapshotResponse> MoveBookingAsync(
        Guid tripId,
        Guid bookingOrderId,
        MoveBookingGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<TripGroupingSnapshotResponse> SplitBookingAsync(
        Guid tripId,
        Guid bookingOrderId,
        SplitTripBookingRequest request,
        CancellationToken cancellationToken = default);

    Task<RecalculateTripProvidedItemsResponse> RecalculateProvidedItemsAsync(
        Guid tripId,
        PublishTripGroupingRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<TripGroupingSnapshotResponse> PublishGroupsAsync(
        Guid tripId,
        PublishTripGroupingRequest? request = null,
        CancellationToken cancellationToken = default);

    // 7.4 Danh sách group được giao cho Guide (bao gồm cả Draft)
    Task<IReadOnlyList<GuideTripGroupResponse>> GetGuideGroupsAsync(
        CancellationToken cancellationToken = default);

    Task<GuideTripGroupResponse> GetGuideGroupByIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    // 7.4 Customer view (Pending trước khi chốt, Published sau khi chốt; lọc participant theo quyền xem)
    Task<CustomerTripGroupingResponse> GetCustomerGroupingAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
