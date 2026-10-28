namespace RenTrek.BLL.Exceptions;

/// <summary>
/// Base cho mọi exception nghiệp vụ. ExceptionHandlingMiddleware dựa vào StatusCode để trả response.
/// </summary>
public abstract class AppException : Exception
{
    public int StatusCode { get; }

    /// <summary>Mã lỗi nghiệp vụ (xem ErrorCodes); middleware trả trong errors.code[0]. Null nếu không có.</summary>
    public string? ErrorCode { get; }

    protected AppException(string message, int statusCode, string? errorCode = null) : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
