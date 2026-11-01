using System.Text.Json;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Implementations.Refunds;

internal static class RefundRequestMapping
{
    public static RefundRequestResponse Map(RefundRequest r, IPaymentDataProtector protector, bool detail = true, bool staff = false)
    {
        var policy = RefundPolicyEvaluator.Snapshot(r);
        return new RefundRequestResponse
        {
            Id = r.RefundRequestId, OrderId = r.OrderId, Amount = r.Amount, Status = r.Status,
            ReasonCode = r.ReasonCode, Note = r.Note, MaskedAccount = r.MaskedAccount,
            BankDetails = detail ? JsonSerializer.Deserialize<RefundBankDetailsResponse>(protector.Unprotect(r.ProtectedBankDetails)) : null,
            SubmittedAt = r.SubmittedAt, ReviewedAt = r.ReviewedAt, AssignedStaffId = staff ? r.AssignedStaffId : null,
            RejectReason = r.RejectReason, CompletedAt = r.CompletedAt, TransferReference = r.TransferReference,
            TransferredAt = r.TransferredAt, EvidenceReference = staff && detail ? r.EvidenceReference : null,
            PolicyVersion = policy.Version, RefundPercentage = policy.Percentage
        };
    }
}
