using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Mappings;

/// <summary>Map chính sách hoàn tiền sang dạng công khai cho khách.</summary>
public static class RefundPolicyMapper
{
    public static TripRefundPolicyResponse ToTripPolicy(RefundPolicy policy) => new()
    {
        RefundPolicyId = policy.RefundPolicyId,
        Name = policy.Name,
        Version = policy.Version,
        Description = policy.Description,
        Rules = policy.RefundPolicyRules
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.MinHoursBefore)
            .Select(r => new TripRefundPolicyResponse.RuleItem
            {
                MinHoursBefore = r.MinHoursBefore,
                RefundPercentage = r.RefundPercentage
            })
            .ToList(),
        Reschedule = policy.RescheduleMinHoursBefore is { } minHours && policy.MaxReschedules > 0
            ? new TripRefundPolicyResponse.RescheduleTerms
            {
                MinHoursBefore = minHours,
                Fee = policy.RescheduleFee,
                MaxTimes = policy.MaxReschedules
            }
            : null,
        NoShowGraceMinutes = policy.NoShowGraceMinutes
    };
}
