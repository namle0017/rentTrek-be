namespace RenTrek.BLL.Exceptions;

/// <summary>
/// Biểu diễn lỗi not found trong tầng business.
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message = "Không tìm thấy dữ liệu") : base(message, 404)
    {
    }
}
