namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị posts.post_type (Hướng dẫn / Kỹ năng / Địa điểm / Tin tức / Blog / Tuyển dụng).
/// Cột varchar không có CHECK constraint nên thêm loại chỉ cần sửa ở đây.
/// </summary>
public static class PostTypes
{
    public const string Guide = "Guide";
    public const string Skill = "Skill";
    public const string Location = "Location";
    public const string News = "News";
    public const string Blog = "Blog";
    public const string Recruitment = "Recruitment";

    public static readonly string[] All = [Guide, Skill, Location, News, Blog, Recruitment];
}
