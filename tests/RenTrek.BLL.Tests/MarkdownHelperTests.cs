using RenTrek.BLL.Helpers;

namespace RenTrek.BLL.Tests;

public sealed class MarkdownHelperTests
{
    [Fact]
    public void ToPlainText_StripsMarkdownSyntax()
    {
        const string markdown = """
            ## Chuẩn bị hành lý
            Mang **áo mưa** và _giày_ chống trượt.
            ![Đỉnh Fansipan](https://cdn.example/img.jpg)
            - Xem [bản đồ](https://maps.example) trước khi đi.
            > Không đi một mình.
            """;

        Assert.Equal(
            "Mang áo mưa và giày chống trượt. Xem bản đồ trước khi đi. Không đi một mình.",
            MarkdownHelper.ToPlainText(markdown));
    }

    [Fact]
    public void ToPlainText_KeepsLegacyPlainTextPosts()
    {
        Assert.Equal("Đoạn một. Đoạn hai.", MarkdownHelper.ToPlainText("## Mục\nĐoạn một.\n\nĐoạn hai."));
    }
}
