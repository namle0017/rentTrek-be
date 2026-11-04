namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminPostDetailResponse(AdminPostResponse Post, string Content, List<Guid> TagIds, List<Guid> SoftSkillIds);
