namespace RenTrek.BLL.Exceptions;

/// <summary>
/// Biểu diễn lỗi conflict trong tầng business.
/// </summary>
public class ConflictException : AppException
{
    public ConflictException(string message = "Dữ liệu đã tồn tại") : base(message, 409)
    {
    }
}
