using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Màn hình group của Guide và màn hình chia đoàn của Khách.</summary>
public interface ITripGroupViewService
{
    Task<IReadOnlyList<GuideTripGroupResponse>> GetGuideGroupsAsync(
        CancellationToken cancellationToken = default);

    Task<GuideTripGroupResponse> GetGuideGroupByIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task<CustomerTripGroupingResponse> GetCustomerGroupingAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
