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
            rowNumber++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
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
