using RenTrek.BLL.Helpers;

namespace RenTrek.BLL.Tests;

public sealed class MarkdownHelperTests
{
    [Fact]
    public void ToPlainText_StripsMarkdownSyntax()
    {
        const string Markdown = """
            ## Chuẩn bị hành lý
            Mang **áo mưa** và _giày_ chống trượt.
            ![Đỉnh Fansipan](https://cdn.example/img.jpg)
            - Xem [bản đồ](https://maps.example) trước khi đi.
            > Không đi một mình.
            """;

        Assert.Equal(
            "Mang áo mưa và giày chống trượt. Xem bản đồ trước khi đi. Không đi một mình.",
            MarkdownHelper.ToPlainText(Markdown));
    }

    [Theory]
    [InlineData("Mở bài\n![Đỉnh](https://cdn.example/a.jpg \"tiêu đề\")\n![x](https://cdn.example/b.jpg)", "https://cdn.example/a.jpg")]
    [InlineData("Không có ảnh, chỉ [link](https://x.example)", null)]
    [InlineData("", null)]
    public void FirstImageUrl_ReturnsFirstMarkdownImage(string markdown, string? expected)
    {
        Assert.Equal(expected, MarkdownHelper.FirstImageUrl(markdown));
    }

    [Fact]
    public void ToPlainText_KeepsLegacyPlainTextPosts()
    {
        Assert.Equal("Đoạn một. Đoạn hai.", MarkdownHelper.ToPlainText("## Mục\nĐoạn một.\n\nĐoạn hai."));
    }

    [Fact]
    public void ToPlainText_StripsHtmlFromRichTextPosts()
    {
        const string Html = "<h2>Chuẩn bị</h2><p>Mang <strong>áo mưa</strong> &amp; <span style=\"color: #e11d48\">giày</span>.</p>"
                            + "<figure><img src=\"https://cdn.example/a.jpg\" alt=\"x\"></figure><ul><li><p>Đèn pin</p></li></ul>";

        Assert.Equal("Mang áo mưa & giày . Đèn pin", MarkdownHelper.ToPlainText(Html));
    }

    [Theory]
    [InlineData("<p>Mở bài</p><img src=\"https://cdn.example/a.jpg?w=1&amp;h=2\"><img src=\"https://cdn.example/b.jpg\">", "https://cdn.example/a.jpg?w=1&h=2")]
    [InlineData("<p>Không có ảnh</p>", null)]
    public void FirstImageUrl_ReadsHtmlImages(string html, string? expected)
    {
        Assert.Equal(expected, MarkdownHelper.FirstImageUrl(html));
    }
}
