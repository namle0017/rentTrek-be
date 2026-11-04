namespace RenTrek.BLL.DTOs.Admin.Requests;

/// <summary>Cùng dữ liệu với <see cref="CreatePromotionRequest"/>.</summary>
public sealed record UpdatePromotionRequest(string Name, string? PromotionCode, string? Description, DateTime StartAt, DateTime EndAt, string AppliesTo, string ApplicationScope, int? MaxUses, int? MaxUsesPerUser, bool IsStackable, int Priority) : CreatePromotionRequest(Name, PromotionCode, Description, StartAt, EndAt, AppliesTo, ApplicationScope, MaxUses, MaxUsesPerUser, IsStackable, Priority);
