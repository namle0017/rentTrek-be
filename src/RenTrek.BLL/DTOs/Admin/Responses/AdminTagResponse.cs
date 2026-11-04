namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminTagResponse(Guid TagId, string Name, string Slug, int PostsCount);
