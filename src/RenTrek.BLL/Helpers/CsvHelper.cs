using System.Globalization;
using System.Text;

namespace RenTrek.BLL.Helpers;

/// <summary>Tiện ích chuẩn hóa ô CSV theo RFC 4180 và mã hóa UTF-8 có BOM để mở đúng tiếng Việt trong Excel.</summary>
public static class CsvHelper
{
    /// <summary>
    /// Bao ngoặc kép và nhân đôi dấu nháy bên trong nếu giá trị khác rỗng.
    /// Giá trị bắt đầu bằng = + - @ tab CR (không phải số) được thêm ' ở đầu để Excel không chạy như công thức (CSV injection).
    /// </summary>
    public static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if ("=+-@\t\r".Contains(value[0], StringComparison.Ordinal)
            && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            value = "'" + value;

        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    /// <summary>Ghép danh sách cột đã escape thành một dòng CSV ngăn cách bằng dấu phẩy.</summary>
    public static string BuildRow(params string?[] fields)
        => string.Join(',', fields.Select(EscapeCsv));

    /// <summary>Chuyển nội dung CSV sang mảng byte UTF-8 kèm BOM ở đầu file.</summary>
    public static byte[] ToUtf8WithBom(string csvContent)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(csvContent);
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }
}
