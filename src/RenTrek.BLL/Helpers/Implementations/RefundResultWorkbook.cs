namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>Kết quả đọc file Excel kế toán gửi lại.</summary>
public sealed class RefundResultWorkbook
{
    public List<RefundResultRow> Rows { get; set; } = [];

    /// <summary>Tên header bắt buộc mà file không có (rỗng nếu file hợp lệ).</summary>
    public List<string> MissingHeaders { get; set; } = [];
}
