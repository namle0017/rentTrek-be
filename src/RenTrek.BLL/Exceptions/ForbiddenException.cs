namespace RenTrek.BLL.Exceptions;

/// <summary>
/// Biểu diễn lỗi forbidden trong tầng business.
/// </summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này", string? errorCode = null)
        : base(message, 403, errorCode)
    {
    }
}
