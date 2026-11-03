namespace RenTrek.BLL.Options;

/// <summary>
/// Chứa khóa bí mật dùng ký link mời nhóm. Đổi khóa sẽ làm mọi link đang có hết hiệu lực.
/// </summary>
public class InviteSettings
{
    public const string SectionName = "Invite";

    /// <summary>Tối thiểu 32 ký tự, không dùng chung với Jwt:SecretKey.</summary>
    public string SigningKey { get; set; } = string.Empty;
}
