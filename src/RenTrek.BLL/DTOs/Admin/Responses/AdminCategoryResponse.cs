namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminCategoryResponse(Guid CategoryId, string Name, string CategoryType, string? Description, bool IsActive, DateTime CreatedAt);
