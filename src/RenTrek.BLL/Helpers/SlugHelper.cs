using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Sinh slug URL từ chuỗi tiếng Việt: bỏ dấu, chữ thường, nối bằng gạch ngang.
/// "Chuẩn bị gì cho Fansipan?" → "chuan-bi-gi-cho-fansipan".
/// </summary>
public static partial class SlugHelper
{
    public static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant()
            .Replace('đ', 'd')
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return NonSlugChars().Replace(builder.ToString(), "-").Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}
