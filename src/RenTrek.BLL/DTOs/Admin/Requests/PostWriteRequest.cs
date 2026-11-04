namespace RenTrek.BLL.DTOs.Admin.Requests;

public sealed record PostWriteRequest(string Title, string Slug, string PostType, string Content, List<Guid> TagIds, List<Guid> SoftSkillIds);
