namespace RenTrek.BLL.Exceptions;

/// <summary>
/// Base cho mọi exception nghiệp vụ. ExceptionHandlingMiddleware dựa vào StatusCode để trả response.
/// </summary>
public abstract class AppException : Exception
{
    public int StatusCode { get; }

    protected AppException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
