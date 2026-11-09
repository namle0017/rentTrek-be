namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc, sắp xếp và phân trang nhật ký audit ở trang quản trị.
/// </summary>
public class AuditLogFilter
{
    /// <summary>Log do người này thực hiện hoặc tác động lên tài khoản của họ.</summary>
    public Guid? RelatedUserId { get; set; }

    public Guid? ActorUserId { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string? Action { get; set; }

    /// <summary>CreatedAt từ thời điểm này trở đi (gồm cả mốc).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>CreatedAt đến thời điểm này (gồm cả mốc).</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Chỉ hỗ trợ CreatedAt.</summary>
    public string? SortBy { get; set; }

    public bool IsDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
