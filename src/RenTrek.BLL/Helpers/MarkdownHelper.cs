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
