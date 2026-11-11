namespace RenTrek.BLL.DTOs.Admin.Requests;

/// <summary>Cùng dữ liệu với <see cref="CreateCategoryRequest"/>.</summary>
public sealed record UpdateCategoryRequest(string Name, string? Description, string? CategoryType, bool IsActive = true) : CreateCategoryRequest(Name, Description, CategoryType, IsActive);
