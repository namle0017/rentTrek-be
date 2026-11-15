using System.Text.RegularExpressions;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Đổi nội dung Markdown của bài viết thành văn bản thuần để làm đoạn trích:
/// bỏ dòng tiêu đề, bỏ ảnh, giữ chữ của link, bỏ ký hiệu **, _, `, ~, &gt;, -.
/// </summary>
public static partial class MarkdownHelper
{
    public static string ToPlainText(string markdown)
    {
        var lines = markdown
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#'))
            .Select(line => ListOrQuoteMarker().Replace(line, string.Empty));

        var text = string.Join(' ', lines);
        text = Image().Replace(text, string.Empty);
        text = Link().Replace(text, "$1");
        text = Emphasis().Replace(text, string.Empty);
        return Spaces().Replace(text, " ").Trim();
    }

    /// <summary>URL của ảnh Markdown đầu tiên (<c>![mô tả](url)</c>) dùng làm ảnh bìa; null nếu bài không có ảnh.</summary>
    public static string? FirstImageUrl(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return null;

        var match = FirstImage().Match(markdown);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"!\[[^\]]*\]\(\s*<?([^\s)>]+)")]
    private static partial Regex FirstImage();

    [GeneratedRegex(@"^(?:[>\-+*]|\d+\.)\s+")]
    private static partial Regex ListOrQuoteMarker();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex Image();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"[*_`~]")]
    private static partial Regex Emphasis();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
