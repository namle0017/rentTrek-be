namespace RenTrek.BLL.Exceptions;

/// <summary>
/// Biểu diễn lỗi conflict trong tầng business.
/// </summary>
public class ConflictException : AppException
{
    /// <summary>Chi tiết lỗi bổ sung (ví dụ danh sách tài nguyên không còn khả dụng); null nếu không có.</summary>
    public IDictionary<string, string[]>? Errors { get; }

    public ConflictException(
        string message = "Dữ liệu đã tồn tại",
        string? errorCode = null,
        IDictionary<string, string[]>? errors = null)
        : base(message, 409, errorCode)
    {
        Errors = errors;
    }
}
