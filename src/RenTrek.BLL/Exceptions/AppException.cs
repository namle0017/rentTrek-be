namespace RenTrek.BLL.Exceptions;

public abstract class AppException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
