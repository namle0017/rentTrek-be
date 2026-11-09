namespace RenTrek.BLL.DTOs.Admin.Requests;

/// <summary>Cùng dữ liệu với <see cref="CreateTagRequest"/>.</summary>
public sealed record UpdateTagRequest(string Name, string Slug) : CreateTagRequest(Name, Slug);
