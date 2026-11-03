namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị posts.status.
/// </summary>
public static class PostStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";

    public static readonly string[] All = [Draft, Published];
}

/// <summary>
/// Giá trị posts.post_type (Hướng dẫn / Kỹ năng / Tin tức).
/// </summary>
public static class PostTypes
{
    public const string Guide = "Guide";
    public const string Skill = "Skill";
    public const string News = "News";

    public static readonly string[] All = [Guide, Skill, News];
}
