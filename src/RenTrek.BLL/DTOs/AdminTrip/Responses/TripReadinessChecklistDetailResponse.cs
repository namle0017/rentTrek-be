namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Chi tiết 5 điều kiện trong Readiness Checklist (8.5).
/// </summary>
public sealed class TripReadinessChecklistDetailResponse
{
    public ReadinessMinParticipantsItemResponse MinParticipantsMet { get; set; } = new();
    public ReadinessParticipantsGroupedItemResponse AllParticipantsGrouped { get; set; } = new();
    public ReadinessGroupsGuidedItemResponse AllGroupsHaveGuides { get; set; } = new();
    public ReadinessProvidedItemsItemResponse AllProvidedItemsReady { get; set; } = new();
    public ReadinessOrdersPaidItemResponse AllOrdersPaid { get; set; } = new();
}
