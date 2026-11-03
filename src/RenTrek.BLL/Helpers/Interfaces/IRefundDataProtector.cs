namespace RenTrek.BLL.Helpers.Interfaces;

public sealed record RefundPayoutDetails(string AccountNumber, string AccountHolder);

public sealed record ProtectedRefundPayload(byte[] Ciphertext, byte[] Nonce, byte[] Tag);

public interface IRefundDataProtector
{
    ProtectedRefundPayload Protect(RefundPayoutDetails details, Guid refundCaseId, Guid orderId);
    RefundPayoutDetails Unprotect(ProtectedRefundPayload payload, Guid refundCaseId, Guid orderId);
}
