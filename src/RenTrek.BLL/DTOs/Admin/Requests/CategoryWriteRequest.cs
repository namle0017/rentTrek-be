namespace RenTrek.BLL.DTOs.Admin.Requests;

public sealed record CategoryWriteRequest(string Name, string? Description, string? CategoryType, bool IsActive = true);
