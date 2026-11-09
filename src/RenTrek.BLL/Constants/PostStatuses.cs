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
