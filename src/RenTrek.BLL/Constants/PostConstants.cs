namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị posts.status.
/// </summary>
public static class PostStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Archived = "Archived";

    public static readonly string[] All = [Draft, Published, Archived];
}

/// <summary>
/// Giá trị posts.post_type (Hướng dẫn / Kỹ năng / Địa điểm / Tin tức).
/// </summary>
public static class PostTypes
{
    public const string Guide = "Guide";
    public const string Skill = "Skill";
    public const string Location = "Location";
    public const string News = "News";

    public static readonly string[] All = [Guide, Skill, Location, News];
}
