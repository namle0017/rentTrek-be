namespace RenTrek.BLL.Exceptions;

/// <summary>
/// Biểu diễn lỗi unauthorized trong tầng business.
/// </summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(
        string message = "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn",
        string? errorCode = null)
        : base(message, 401, errorCode)
    {
    }
}
