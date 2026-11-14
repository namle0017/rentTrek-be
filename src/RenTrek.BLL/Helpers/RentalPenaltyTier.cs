using RenTrek.BLL.Constants;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Helpers;

/// <summary>Một bậc phạt trễ: từ <see cref="MinDaysLate"/> ngày trễ (đủ 24h) thu <see cref="PercentOfDeposit"/>% cọc.</summary>
public sealed record RentalPenaltyTier(int MinDaysLate, decimal PercentOfDeposit, bool IsTerminal);
