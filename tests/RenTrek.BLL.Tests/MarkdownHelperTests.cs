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
}
