using RenTrek.BLL.Constants;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Helpers;

/// <summary>Trạng thái trễ trả tại một thời điểm (chưa gồm tiền thuê ngày trễ).</summary>
public sealed record RentalReturnStatus(int DaysLate, RentalPenaltyTier? Tier, decimal PenaltyFee, bool IsSuspectedLoss);
