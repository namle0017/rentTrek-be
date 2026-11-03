namespace RenTrek.BLL.Exceptions;

/// <summary>
/// Biểu diễn lỗi vượt giới hạn tần suất trong tầng business.
/// </summary>
public class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string message = "Bạn thao tác quá nhanh, vui lòng thử lại sau", string? errorCode = null)
        : base(message, 429, errorCode)
    {
    }
}
