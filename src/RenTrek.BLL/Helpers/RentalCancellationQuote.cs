using RenTrek.BLL.Constants;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Helpers;

/// <summary>Kết quả báo giá huỷ đơn đã thanh toán.</summary>
public sealed record RentalCancellationQuote(
    bool Allowed,
    string CancellationType,
    int DaysLate,
    decimal Fee,
    decimal RefundAmount,
    RentalPenaltyTier? AppliedTier,
    int FreeCancellationsUsed,
    string? BlockedReason);
