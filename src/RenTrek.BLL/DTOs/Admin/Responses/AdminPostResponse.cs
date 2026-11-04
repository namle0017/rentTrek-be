namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminPostResponse(Guid PostId, string Title, string Slug, string PostType, string AuthorFullName, string Status, DateTime? PublishedAt, int LinkedToursCount, int ReadCount);
