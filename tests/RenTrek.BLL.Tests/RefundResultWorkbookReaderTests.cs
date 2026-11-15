using ClosedXML.Excel;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Tests.Fixtures;

namespace RenTrek.BLL.Tests;

/// <summary>Một dòng kết quả kế toán dùng để dựng workbook test.</summary>
public sealed class ResultRowSpec
{
    public Guid? RefundCaseId { get; init; }
    public string Code { get; init; } = "HT-00000000";
    public string? Transferred { get; init; }
    public string? TransactionId { get; init; }
    public string? Reason { get; init; }
    /// <summary>Ảnh nổi (floating) neo vào ô "Hình ảnh minh chứng" của dòng này.</summary>
    public IReadOnlyList<byte[]> FloatingImages { get; init; } = [];
    /// <summary>Ảnh Place in Cell: chỉ một ảnh mỗi ô.</summary>
    public byte[]? PlaceInCellImage { get; init; }
}

/// <summary>Dựng file xlsx giống file xuất cho kế toán + 4 cột kết quả, hoàn toàn trong bộ nhớ.</summary>
public static class ResultWorkbookFactory
{
    public static readonly string[] ExportHeaders =
        ["Mã hồ sơ", "Mã đơn", "Loại đơn", "Nguồn hoàn", "Khách hàng", "Ngân hàng", "Số tài khoản", "Chủ tài khoản", "Số tiền (VND)", "Nội dung chuyển khoản", "Ngày tạo"];

    public static readonly string[] ResultHeaders = ["Đã chuyển", "Mã giao dịch", "Lý do chưa chuyển", "Hình ảnh minh chứng"];

    public const string HiddenIdHeader = "RefundCaseId";

    /// <summary>Cột (1-based) của "Hình ảnh minh chứng" khi dùng bố cục chuẩn: 11 cột xuất + RefundCaseId + 4 cột kết quả.</summary>
    public const int ImageColumn = 16;

    public static byte[] Build(
        IEnumerable<ResultRowSpec> rows,
        bool includeHiddenId = true,
        Func<string, bool>? includeHeader = null,
        bool reverseResultColumns = false,
        Action<IXLWorksheet>? customize = null)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Hoàn tiền");
        var headers = ExportHeaders.ToList();
        if (includeHiddenId) headers.Add(HiddenIdHeader);
        headers.AddRange(reverseResultColumns ? ResultHeaders.Reverse() : ResultHeaders);
        headers = headers.Where(h => includeHeader?.Invoke(h) ?? true).ToList();
        for (var i = 0; i < headers.Count; i++) sheet.Cell(1, i + 1).Value = headers[i];
        if (includeHiddenId && headers.Contains(HiddenIdHeader)) sheet.Column(headers.IndexOf(HiddenIdHeader) + 1).Hide();

        int Col(string header) => headers.IndexOf(header) + 1;
        var rowNumber = 2;
        var placeInCell = new Dictionary<string, byte[]>();
        foreach (var spec in rows)
        {
            if (Col("Mã hồ sơ") > 0) sheet.Cell(rowNumber, Col("Mã hồ sơ")).Value = spec.Code;
            if (Col("Khách hàng") > 0) sheet.Cell(rowNumber, Col("Khách hàng")).Value = "Nguyễn Văn A";
            if (Col("Số tiền (VND)") > 0) sheet.Cell(rowNumber, Col("Số tiền (VND)")).Value = 2_000_000;
            if (spec.RefundCaseId is { } id && Col(HiddenIdHeader) > 0) sheet.Cell(rowNumber, Col(HiddenIdHeader)).Value = id.ToString();
            if (spec.Transferred is not null && Col("Đã chuyển") > 0) sheet.Cell(rowNumber, Col("Đã chuyển")).Value = spec.Transferred;
            if (spec.TransactionId is not null && Col("Mã giao dịch") > 0)
            {
                sheet.Cell(rowNumber, Col("Mã giao dịch")).Style.NumberFormat.Format = "@";
                sheet.Cell(rowNumber, Col("Mã giao dịch")).Value = spec.TransactionId;
            }

            if (spec.Reason is not null && Col("Lý do chưa chuyển") > 0) sheet.Cell(rowNumber, Col("Lý do chưa chuyển")).Value = spec.Reason;

            var imageColumn = Col("Hình ảnh minh chứng");
            if (imageColumn > 0)
            {
                var offset = 0;
                foreach (var image in spec.FloatingImages)
                {
                    // Mỗi ảnh nổi neo vào ô của dòng; dịch nhẹ để không chồng nhau.
                    sheet.AddPicture(new MemoryStream(image)).MoveTo(sheet.Cell(rowNumber, imageColumn), offset, 0);
                    offset += 20;
                }

                if (spec.PlaceInCellImage is not null)
                    placeInCell[sheet.Cell(rowNumber, imageColumn).Address.ToStringRelative()] = spec.PlaceInCellImage;
            }

            rowNumber++;
        }

        customize?.Invoke(sheet);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var bytes = stream.ToArray();
        return placeInCell.Count == 0 ? bytes : PlaceInCellWorkbookBuilder.AddImages(bytes, placeInCell);
    }
}

public sealed class RefundResultWorkbookReaderTests
{
    private static readonly Guid CaseA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CaseB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static string Fold(string value)
        => new string(value.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant();

    // ---- Header & hàng ----

    [Fact]
    public void Read_ValidWorkbook_ParsesAllRowFields()
    {
        var workbook = ResultWorkbookFactory.Build(
        [
            new ResultRowSpec { RefundCaseId = CaseA, Code = "HT-11111111", Transferred = "Có", TransactionId = " FT123 ", FloatingImages = [TestImages.Png()] },
            new ResultRowSpec { RefundCaseId = CaseB, Code = "HT-22222222", Transferred = "Không", Reason = " Sai số tài khoản " }
        ]);

        var result = RefundResultWorkbookReader.Read(new MemoryStream(workbook));

        Assert.Empty(result.MissingHeaders);
        Assert.Equal(2, result.Rows.Count());
        var first = result.Rows[0];
        Assert.Equal(2, first.RowNumber);
        Assert.Equal(CaseA, first.RefundCaseId);
        Assert.Equal("HT-11111111", first.RefundCode);
        Assert.Equal("Có", first.TransferredRaw);
        Assert.True(first.Transferred);
        Assert.Equal("FT123", first.TransactionId);
        var second = result.Rows[1];
        Assert.Equal(3, second.RowNumber);
        Assert.False(second.Transferred);
        Assert.Equal("Sai số tài khoản", second.Reason);
        Assert.Empty(second.Images);
    }

    [Theory]
    [InlineData("Có", true)]
    [InlineData("có", true)]
    [InlineData("Co", true)]
    [InlineData("YES", true)]
    [InlineData("x", true)]
    [InlineData("X", true)]
    [InlineData("1", true)]
    [InlineData("Không", false)]
    [InlineData("KHÔNG", false)]
    [InlineData("Khong", false)]
    [InlineData("no", false)]
    [InlineData("0", false)]
    [InlineData("Chờ", null)]
    [InlineData("Maybe", null)]
    public void Read_TransferredValues_AreInterpretedLeniently(string raw, bool? expected)
    {
        var workbook = ResultWorkbookFactory.Build([new ResultRowSpec { RefundCaseId = CaseA, Transferred = raw }]);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Equal(raw, row.TransferredRaw);
        Assert.Equal(expected, row.Transferred);
    }

    [Fact]
    public void Read_BlankTransferred_IsNullAndRowIsKept()
    {
        var workbook = ResultWorkbookFactory.Build([new ResultRowSpec { RefundCaseId = CaseA }]);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Null(row.Transferred);
        Assert.True(string.IsNullOrEmpty(row.TransferredRaw));
    }

    [Fact]
    public void Read_FindsColumnsByNameNotPosition()
    {
        // Đảo thứ tự 4 cột kết quả: reader phải tra theo tên header
        var workbook = ResultWorkbookFactory.Build(
            [new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có", TransactionId = "TX-9", FloatingImages = [] }],
            reverseResultColumns: true);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Equal("TX-9", row.TransactionId);
        Assert.True(row.Transferred);
    }

    [Fact]
    public void Read_HeadersAreCaseAndDiacriticInsensitive()
    {
        var workbook = ResultWorkbookFactory.Build(
            [new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có", TransactionId = "TX-1" }],
            customize: sheet =>
            {
                for (var column = 1; column <= 20; column++)
                {
                    var header = sheet.Cell(1, column).GetString();
                    if (header is "Đã chuyển" or "Mã giao dịch" or "Lý do chưa chuyển")
                        sheet.Cell(1, column).Value = Fold(header).ToUpperInvariant();
                }
            });

        var result = RefundResultWorkbookReader.Read(new MemoryStream(workbook));

        Assert.Empty(result.MissingHeaders);
        var row = Assert.Single(result.Rows);
        Assert.Equal("TX-1", row.TransactionId);
        Assert.True(row.Transferred);
    }

    [Theory]
    [InlineData("Đã chuyển")]
    [InlineData("Mã giao dịch")]
    [InlineData("Lý do chưa chuyển")]
    public void Read_MissingRequiredHeader_IsReported(string missing)
    {
        var workbook = ResultWorkbookFactory.Build(
            [new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có" }],
            includeHeader: header => header != missing);

        var result = RefundResultWorkbookReader.Read(new MemoryStream(workbook));

        Assert.Contains(result.MissingHeaders, name => Fold(name) == Fold(missing));
    }

    [Fact]
    public void Read_TextBeforeAndAfterRows_BlankRowsAreIgnored()
    {
        var workbook = ResultWorkbookFactory.Build(
        [
            new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có", TransactionId = "TX-A" },
            new ResultRowSpec { Code = "" }, // dòng trống hoàn toàn (không id, không dữ liệu)
            new ResultRowSpec { RefundCaseId = CaseB, Transferred = "Không", Reason = "Lỗi" }
        ], customize: sheet =>
        {
            sheet.Cell(3, 1).Clear();
            sheet.Cell(3, 5).Clear();
            sheet.Cell(3, 9).Clear();
        });

        var rows = RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows;

        Assert.Equal([2, 4], rows.Select(r => r.RowNumber).ToArray());
    }

    // ---- Khớp hồ sơ ----

    [Fact]
    public void Read_HiddenIdColumn_GivesFullGuid()
    {
        var workbook = ResultWorkbookFactory.Build([new ResultRowSpec { RefundCaseId = CaseA, Code = "HT-11111111" }]);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Equal(CaseA, row.RefundCaseId);
    }

    [Fact]
    public void Read_NoHiddenIdColumn_FallsBackToCodeOnly()
    {
        var workbook = ResultWorkbookFactory.Build(
            [new ResultRowSpec { Code = "HT-ABCDEF12", Transferred = "Có", TransactionId = "TX" }],
            includeHiddenId: false);

        var result = RefundResultWorkbookReader.Read(new MemoryStream(workbook));

        Assert.Empty(result.MissingHeaders); // thiếu RefundCaseId không phải lỗi header
        var row = Assert.Single(result.Rows);
        Assert.Null(row.RefundCaseId);
        Assert.Equal("HT-ABCDEF12", row.RefundCode);
    }

    [Fact]
    public void Read_InvalidGuidInHiddenColumn_IsNullNotException()
    {
        var workbook = ResultWorkbookFactory.Build([new ResultRowSpec { Code = "HT-11111111", Transferred = "Có" }],
            customize: sheet => sheet.Cell(2, 12).Value = "not-a-guid");

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Null(row.RefundCaseId);
    }

    [Fact]
    public void Read_TransactionIdKeepsLeadingZerosAndNumbers()
    {
        var workbook = ResultWorkbookFactory.Build(
        [
            new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có", TransactionId = "000123456" }
        ]);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Equal("000123456", row.TransactionId);
    }

    [Fact]
    public void Read_NumericTransactionCell_IsReadAsText()
    {
        var workbook = ResultWorkbookFactory.Build([new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có" }],
            customize: sheet => sheet.Cell(2, 14).Value = 987654321);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Equal("987654321", row.TransactionId);
    }

    // ---- Ảnh ----

    [Fact]
    public void Read_FloatingPicture_IsAttachedToItsAnchorRow()
    {
        var red = TestImages.Png(255, 0, 0);
        var blue = TestImages.Png(0, 0, 255);
        var workbook = ResultWorkbookFactory.Build(
        [
            new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có", TransactionId = "A", FloatingImages = [red] },
            new ResultRowSpec { RefundCaseId = CaseB, Transferred = "Có", TransactionId = "B", FloatingImages = [blue] }
        ]);

        var rows = RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows;

        var imageA = Assert.Single(rows[0].Images);
        var imageB = Assert.Single(rows[1].Images);
        Assert.Equal("image/png", imageA.ContentType);
        Assert.Equal(TestImages.PngSignature, imageA.Data.Take(8).ToArray());
        Assert.False(string.IsNullOrWhiteSpace(imageA.FileName));
        Assert.False(imageA.Data.SequenceEqual(imageB.Data)); // không lẫn ảnh giữa các dòng
    }

    [Fact]
    public void Read_MultipleFloatingPicturesInOneRow_AllReturned()
    {
        var workbook = ResultWorkbookFactory.Build(
        [
            new ResultRowSpec
            {
                RefundCaseId = CaseA, Transferred = "Có", TransactionId = "A",
                FloatingImages = [TestImages.Png(255, 0, 0), TestImages.Png(0, 255, 0), TestImages.Png(0, 0, 255)]
            }
        ]);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Equal(3, row.Images.Count());
    }

    [Fact]
    public void Read_MoreThanThreePictures_ReaderReturnsAllAndLeavesLimitToService()
    {
        // Giả định: giới hạn 3 ảnh do service kiểm tra để báo lỗi theo dòng; reader không cắt bớt.
        var images = Enumerable.Range(0, 4).Select(i => TestImages.Png((byte)(i * 60), 10, 10)).ToList();
        var workbook = ResultWorkbookFactory.Build(
            [new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có", TransactionId = "A", FloatingImages = images }]);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Equal(4, row.Images.Count());
    }

    [Fact]
    public void Read_PlaceInCellPicture_IsExtractedByteForByte()
    {
        var image = TestImages.Png(10, 200, 30, 8, 8);
        var workbook = ResultWorkbookFactory.Build(
            [new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có", TransactionId = "A", PlaceInCellImage = image }]);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        var extracted = Assert.Single(row.Images);
        Assert.Equal(image, extracted.Data);
        Assert.Equal("image/png", extracted.ContentType);
    }

    [Fact]
    public void Read_PlaceInCell_EachRowGetsItsOwnImage()
    {
        var first = TestImages.Png(1, 2, 3);
        var second = TestImages.Png(200, 100, 50);
        var workbook = ResultWorkbookFactory.Build(
        [
            new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có", TransactionId = "A", PlaceInCellImage = first },
            new ResultRowSpec { RefundCaseId = CaseB, Transferred = "Có", TransactionId = "B", PlaceInCellImage = second }
        ]);

        var rows = RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows;

        Assert.Equal(first, Assert.Single(rows[0].Images).Data);
        Assert.Equal(second, Assert.Single(rows[1].Images).Data);
    }

    [Fact]
    public void Read_MixOfFloatingAndPlaceInCellRows_BothSupported()
    {
        var floating = TestImages.Png(255, 255, 0);
        var inCell = TestImages.Png(0, 255, 255);
        var workbook = ResultWorkbookFactory.Build(
        [
            new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có", TransactionId = "A", FloatingImages = [floating] },
            new ResultRowSpec { RefundCaseId = CaseB, Transferred = "Có", TransactionId = "B", PlaceInCellImage = inCell }
        ]);

        var rows = RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows;

        Assert.Single(rows[0].Images);
        Assert.Equal(inCell, Assert.Single(rows[1].Images).Data);
    }

    [Fact]
    public void Read_RowWithoutPictureAndTransferredNo_HasNoImages()
    {
        var workbook = ResultWorkbookFactory.Build(
            [new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Không", Reason = "Sai STK" }]);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Empty(row.Images);
    }

    [Fact]
    public void Read_PlaceInCellWithNonImageBytes_DoesNotThrow()
    {
        // Reader chỉ trích byte; việc kiểm tra magic bytes thuộc service (xem RefundProofServiceTests).
        var workbook = ResultWorkbookFactory.Build(
            [new ResultRowSpec { RefundCaseId = CaseA, Transferred = "Có", TransactionId = "A", PlaceInCellImage = TestImages.NotAnImage() }]);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.NotNull(row.Images);
    }

    // ---- Lỗi file ----

    [Fact]
    public void Read_NotAZip_Throws()
    {
        var garbage = new MemoryStream(TestImages.NotAnImage());

        Assert.ThrowsAny<Exception>(() => RefundResultWorkbookReader.Read(garbage));
    }

    [Fact]
    public void Read_EmptyWorkbookWithHeadersOnly_HasNoRows()
    {
        var workbook = ResultWorkbookFactory.Build([]);

        var result = RefundResultWorkbookReader.Read(new MemoryStream(workbook));

        Assert.Empty(result.MissingHeaders);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Read_ExportedTemplateWithPlaceInCellImageCell_StillReadsTextColumns()
    {
        var workbook = ResultWorkbookFactory.Build(
            [new ResultRowSpec { RefundCaseId = CaseA, Code = "HT-11111111", Transferred = "Có", TransactionId = "FT-77", Reason = null, PlaceInCellImage = TestImages.Png() }]);

        var row = Assert.Single(RefundResultWorkbookReader.Read(new MemoryStream(workbook)).Rows);

        Assert.Equal("FT-77", row.TransactionId);
        Assert.Equal(CaseA, row.RefundCaseId);
    }
}
