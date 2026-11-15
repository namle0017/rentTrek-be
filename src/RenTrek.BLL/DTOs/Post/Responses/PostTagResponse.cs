namespace RenTrek.BLL.DTOs.Post.Responses;

/// <summary>TagId có trong taxonomy để trình soạn bài gửi tagIds; danh sách bài công khai có thể để null.</summary>
public sealed record PostTagResponse(string Name, string Slug, Guid? TagId = null);
