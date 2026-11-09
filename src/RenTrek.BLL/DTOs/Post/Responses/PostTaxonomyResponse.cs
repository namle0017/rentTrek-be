namespace RenTrek.BLL.DTOs.Post.Responses;

public sealed record PostTaxonomyResponse(
    IReadOnlyList<PostTagResponse> Tags,
    IReadOnlyList<PostSoftSkillResponse> SoftSkills);
