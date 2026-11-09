namespace RenTrek.BLL.DTOs.Admin.Requests;

public record CreatePromotionRequest(string Name, string? PromotionCode, string? Description, DateTime StartAt, DateTime EndAt, string AppliesTo, string ApplicationScope, int? MaxUses, int? MaxUsesPerUser, bool IsStackable, int Priority);
