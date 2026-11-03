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
        ["Mã hồ sơ", "Mã đơn", "Khách hàng", "Ngân hàng", "Số tài khoản", "Chủ tài khoản", "Số tiền (VND)", "Nội dung chuyển khoản", "Ngày tạo"];

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static byte[] Build(IEnumerable<(RefundCase Refund, RefundPayoutDetails Details)> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Hoàn tiền");
        for (var column = 0; column < Headers.Length; column++)
            sheet.Cell(1, column + 1).Value = Headers[column];
        sheet.Row(1).Style.Font.Bold = true;

        // Gán chuỗi vào Value luôn ra ô text: giữ số 0 đầu và không chạy tên kiểu "=HYPERLINK(...)" thành công thức
        var rowNumber = 2;
        foreach (var (refund, details) in rows)
        {
            sheet.Cell(rowNumber, 1).Value = $"HT-{refund.RefundCaseId:N}"[..11].ToUpperInvariant();
            sheet.Cell(rowNumber, 2).Value = refund.Order.OrderCode;
            sheet.Cell(rowNumber, 3).Value = refund.CustomerUser.FullName;
            sheet.Cell(rowNumber, 4).Value = RefundBankCodes.Names.GetValueOrDefault(refund.BankCode, refund.BankCode);
            sheet.Cell(rowNumber, 5).Style.NumberFormat.Format = "@";
            sheet.Cell(rowNumber, 5).Value = details.AccountNumber;
            sheet.Cell(rowNumber, 6).Value = details.AccountHolder;
            sheet.Cell(rowNumber, 7).Value = refund.Amount;
            sheet.Cell(rowNumber, 7).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(rowNumber, 8).Value = $"Hoan tien don {refund.Order.OrderCode}";
            sheet.Cell(rowNumber, 9).Value = refund.CreatedAt.ToOffset(VietnamOffset).ToString("dd/MM/yyyy HH:mm");
            rowNumber++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
