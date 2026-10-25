namespace RenTrek.BLL.Exceptions;

/// <summary>
/// Biểu diễn lỗi bad request trong tầng business.
/// </summary>
public class BadRequestException : AppException
{
    public BadRequestException(string message = "Yêu cầu không hợp lệ") : base(message, 400)
    {
    }
}
