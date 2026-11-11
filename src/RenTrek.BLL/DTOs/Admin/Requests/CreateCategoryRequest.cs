namespace RenTrek.BLL.DTOs.Admin.Requests;

public record CreateCategoryRequest(string Name, string? Description, string? CategoryType, bool IsActive = true);
