namespace RenTrek.BLL.DTOs.Admin.Requests;

/// <summary>Cùng dữ liệu với <see cref="CreateSoftSkillRequest"/>.</summary>
public sealed record UpdateSoftSkillRequest(string Name, string? Description) : CreateSoftSkillRequest(Name, Description);
