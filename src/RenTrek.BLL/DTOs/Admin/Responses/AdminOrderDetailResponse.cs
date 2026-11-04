namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminOrderDetailResponse(AdminOrderResponse Order, List<AdminParticipantResponse> Participants, List<AdminPaymentResponse> Payments, List<AdminRentalLineResponse> Lines, List<AdminPromotionUsageResponse> Promotions, List<AdminAuditResponse> AuditLogs, AdminRentalPeriodResponse? RentalPeriod, Guid? HandledByUserId, string? HandlerFullName);
