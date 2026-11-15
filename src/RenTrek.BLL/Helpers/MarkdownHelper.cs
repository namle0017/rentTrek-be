using System.Net;
using System.Text.RegularExpressions;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Đổi nội dung bài viết thành văn bản thuần để làm đoạn trích và lấy ảnh bìa.
/// Bài cũ là Markdown đơn giản (bỏ dòng tiêu đề, bỏ ảnh, giữ chữ của link, bỏ ký hiệu **, _, `, ~, &gt;, -);
/// bài soạn bằng trình soạn WYSIWYG là HTML (bỏ thẻ, bỏ tiêu đề h1–h6, giải mã ký tự HTML).
/// </summary>
public static partial class MarkdownHelper
{
    public static string ToPlainText(string content)
    {
        if (IsHtml(content))
            return HtmlToPlainText(content);

        var lines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#'))
            .Select(line => ListOrQuoteMarker().Replace(line, string.Empty));

        var text = string.Join(' ', lines);
        text = Image().Replace(text, string.Empty);
        text = Link().Replace(text, "$1");
        text = Emphasis().Replace(text, string.Empty);
        return Spaces().Replace(text, " ").Trim();
    }

    /// <summary>
    /// URL ảnh đầu tiên dùng làm ảnh bìa: ảnh Markdown <c>![mô tả](url)</c> hoặc thẻ <c>&lt;img src="url"&gt;</c>,
    /// cái nào xuất hiện trước; null nếu bài không có ảnh.
    /// </summary>
    public static string? FirstImageUrl(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        var markdown = FirstImage().Match(content);
        var html = FirstHtmlImage().Match(content);
        if (markdown.Success && (!html.Success || markdown.Index < html.Index))
            return markdown.Groups[1].Value;
        return html.Success ? WebUtility.HtmlDecode(html.Groups[1].Value) : null;
    }

    /// <summary>Nội dung là HTML khi mở đầu bằng một thẻ (trình soạn WYSIWYG luôn bọc trong &lt;p&gt;, &lt;h2&gt;...).</summary>
    public static bool IsHtml(string? content)
        => !string.IsNullOrEmpty(content) && HtmlStart().IsMatch(content);

    private static string HtmlToPlainText(string html)
    {
        var text = HtmlHeading().Replace(html, " ");
        text = HtmlTag().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        return Spaces().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"!\[[^\]]*\]\(\s*<?([^\s)>]+)")]
    private static partial Regex FirstImage();

    [GeneratedRegex("""<img\b[^>]*?\bsrc\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex FirstHtmlImage();

    [GeneratedRegex(@"^\s*<(?:p|h[1-6]|ul|ol|blockquote|figure|img|div|iframe|hr|pre|table)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlStart();

    [GeneratedRegex(@"<h[1-6]\b[^>]*>.*?</h[1-6]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HtmlHeading();

    [GeneratedRegex(@"<[^>]*>?")]
    private static partial Regex HtmlTag();

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
