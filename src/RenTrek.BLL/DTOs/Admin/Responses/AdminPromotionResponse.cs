namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminPromotionResponse(Guid PromotionId, string Name, string? PromotionCode, string? Description, DateTime StartAt, DateTime EndAt, string AppliesTo, string ApplicationScope, int UsedCount, int? MaxUses, int? MaxUsesPerUser, bool IsStackable, int Priority, string Status);
