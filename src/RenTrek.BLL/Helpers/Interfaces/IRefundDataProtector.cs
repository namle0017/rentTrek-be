namespace RenTrek.BLL.Helpers.Interfaces;

public interface IRefundDataProtector
{
    ProtectedRefundPayload Protect(RefundPayoutDetails details, Guid refundCaseId, Guid orderId);
    RefundPayoutDetails Unprotect(ProtectedRefundPayload payload, Guid refundCaseId, Guid orderId);
}
