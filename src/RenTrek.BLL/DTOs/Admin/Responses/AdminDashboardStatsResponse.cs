namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminDashboardStatsResponse(int PendingPaymentExpiringCount, int TripsDepartingIn7DaysCount, int UnassignedParticipantsCount, int MissingProvidedItemsCount, int UnderCapacityTripsCount, int OverdueRentalAssetsCount, int PendingApprovalRefundsCount, int FailedRefundsCount, int NeedsReconciliationOrdersCount, int DraftRentalChargesCount);
