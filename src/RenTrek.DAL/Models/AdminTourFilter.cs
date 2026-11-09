namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc danh sách tour (bản mới nhất của mỗi họ tour) cho vận hành.
/// </summary>
public class AdminTourFilter
{
    /// <summary>Tìm theo tên (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? LocationId { get; set; }

    /// <summary>Giá trị tours.status được nhận; null là không lọc, rỗng là không trả gì.</summary>
    public IReadOnlyCollection<string>? Statuses { get; set; }

    /// <summary>Mốc đếm chuyến sắp tới.</summary>
    public DateTimeOffset Now { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
