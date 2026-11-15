using System.Globalization;
using ClosedXML.Excel;
using RenTrek.BLL.Constants;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>
/// Tạo file Excel kế toán dùng để chuyển khoản hoàn tiền.
/// </summary>
public static class RefundExportWorkbook
{
    private static readonly string[] Headers =
        ["Mã hồ sơ", "Mã đơn", "Loại đơn", "Nguồn hoàn", "Khách hàng", "Ngân hàng", "Số tài khoản", "Chủ tài khoản", "Số tiền (VND)", "Nội dung chuyển khoản", "Ngày tạo"];

    // Kế toán điền 4 cột này rồi gửi lại file; cột ẩn cuối chứa GUID đầy đủ để hệ thống khớp hồ sơ
    private static readonly string[] ResultHeaders =
        [RefundResultWorkbookReader.TransferredHeader, RefundResultWorkbookReader.TransactionHeader, RefundResultWorkbookReader.ReasonHeader, RefundResultWorkbookReader.ImagesHeader];

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static byte[] Build(IEnumerable<(RefundCase Refund, RefundPayoutDetails Details)> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Hoàn tiền");
        for (var column = 0; column < Headers.Length; column++)
            sheet.Cell(1, column + 1).Value = Headers[column];
        for (var column = 0; column < ResultHeaders.Length; column++)
            sheet.Cell(1, Headers.Length + column + 1).Value = ResultHeaders[column];
        var caseIdColumn = Headers.Length + ResultHeaders.Length + 1;
        sheet.Cell(1, caseIdColumn).Value = RefundResultWorkbookReader.CaseIdHeader;
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Range(1, Headers.Length + 1, 1, caseIdColumn - 1).Style.Fill.BackgroundColor = XLColor.LightYellow;

        // Gán chuỗi vào Value luôn ra ô text: giữ số 0 đầu và không chạy tên kiểu "=HYPERLINK(...)" thành công thức
        var rowNumber = 2;
        foreach (var (refund, details) in rows)
        {
            sheet.Cell(rowNumber, 1).Value = $"HT-{refund.RefundCaseId:N}"[..11].ToUpperInvariant();
            sheet.Cell(rowNumber, 2).Value = refund.Order.OrderCode;
            sheet.Cell(rowNumber, 3).Value = refund.Order.OrderType == OrderTypes.Rental ? "Thuê thiết bị" : "Tour";
            sheet.Cell(rowNumber, 4).Value = SourceLabel(refund.Source);
            sheet.Cell(rowNumber, 5).Value = refund.CustomerUser.FullName;
            var bankCode = refund.BankCode ?? string.Empty;
            sheet.Cell(rowNumber, 6).Value = RefundBankCodes.Names.GetValueOrDefault(bankCode, bankCode);
            sheet.Cell(rowNumber, 7).Style.NumberFormat.Format = "@";
            sheet.Cell(rowNumber, 7).Value = details.AccountNumber;
            sheet.Cell(rowNumber, 8).Value = details.AccountHolder;
            sheet.Cell(rowNumber, 9).Value = refund.Amount;
            sheet.Cell(rowNumber, 9).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(rowNumber, 10).Value = $"Hoan tien don {refund.Order.OrderCode}";
            sheet.Cell(rowNumber, 11).Value = refund.CreatedAt.ToOffset(VietnamOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            sheet.Cell(rowNumber, Headers.Length + 2).Style.NumberFormat.Format = "@";
            sheet.Cell(rowNumber, caseIdColumn).Value = refund.RefundCaseId.ToString("D");
            sheet.Row(rowNumber).Height = 80;
            sheet.Row(rowNumber).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            rowNumber++;
        }

        sheet.Columns().AdjustToContents();
        sheet.Column(Headers.Length + 1).Width = 14;
        sheet.Column(Headers.Length + 2).Width = 28;
        sheet.Column(Headers.Length + 3).Width = 36;
        sheet.Column(Headers.Length + 4).Width = 45;
        if (rowNumber > 2)
        {
            var validation = sheet.Range(2, Headers.Length + 1, rowNumber - 1, Headers.Length + 1).CreateDataValidation();
            validation.List("\"Có,Không\"", true);
            validation.IgnoreBlanks = true;
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorTitle = "Giá trị không hợp lệ";
            validation.ErrorMessage = "Chỉ chọn Có hoặc Không";
        }

        sheet.Column(caseIdColumn).Hide();
        sheet.SheetView.FreezeRows(1);
        AddInstructions(workbook);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddInstructions(XLWorkbook workbook)
    {
        var guide = workbook.Worksheets.Add("Hướng dẫn");
        string[] lines =
        [
            "HƯỚNG DẪN ĐIỀN KẾT QUẢ CHUYỂN KHOẢN HOÀN TIỀN",
            "1. Chuyển khoản cho từng dòng ở sheet \"Hoàn tiền\" theo ngân hàng, số tài khoản, chủ tài khoản và số tiền đã ghi.",
            "2. Điền 4 cột cuối (nền vàng) cho MỖI dòng:",
            "   - Đã chuyển: chọn Có (đã chuyển thành công) hoặc Không (chưa chuyển được).",
            "   - Mã giao dịch: mã giao dịch của ngân hàng. BẮT BUỘC khi chọn Có, không được trùng giữa các dòng.",
            "   - Lý do chưa chuyển: BẮT BUỘC khi chọn Không (tối đa 500 ký tự). Hồ sơ sẽ được trả về để khách nhập lại số tài khoản.",
            "   - Hình ảnh minh chứng: chèn 1 đến 3 ảnh biên lai vào ô (JPG, PNG hoặc WebP, mỗi ảnh tối đa 5MB). BẮT BUỘC khi chọn Có.",
            "3. Không xóa, không đổi tên tiêu đề cột; không xóa cột ẩn RefundCaseId (dùng để khớp hồ sơ).",
            "4. Lưu file .xlsx và gửi lại cho Admin để nhập vào hệ thống. Dòng để trống cột \"Đã chuyển\" sẽ được bỏ qua.",
            "5. Để chèn ảnh: chọn ô ở cột \"Hình ảnh minh chứng\" > Insert > Pictures > Place in Cell (hoặc kéo ảnh đặt lên đúng dòng)."
        ];
        for (var i = 0; i < lines.Length; i++)
            guide.Cell(i + 1, 1).Value = lines[i];
        guide.Cell(1, 1).Style.Font.Bold = true;
        guide.Column(1).Width = 130;
    }

    /// <summary>Nhãn tiếng Việt của nguồn hoàn để kế toán biết khoản này từ đâu.</summary>
    public static string SourceLabel(string? source) => source switch
    {
        RefundCaseSources.RentalCancellation => "Huỷ đơn thuê",
        RefundCaseSources.RentalNoShow => "Không đến nhận (hoàn tiền thuê)",
        RefundCaseSources.RentalDepositReturn => "Hoàn cọc sau khi trả đồ",
        _ => "Huỷ tour"
    };
}
