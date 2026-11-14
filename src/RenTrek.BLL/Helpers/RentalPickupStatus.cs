using RenTrek.BLL.Constants;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Helpers;

/// <summary>Trạng thái trễ nhận tại một thời điểm.</summary>
public sealed record RentalPickupStatus(int DaysLate, RentalPenaltyTier? Tier, bool IsNoShow, decimal Fee);
