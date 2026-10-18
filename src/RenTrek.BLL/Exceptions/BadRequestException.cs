namespace RenTrek.BLL.Exceptions;

public class BadRequestException : AppException
{
    public BadRequestException(string message = "Yêu cầu không hợp lệ") : base(message, 400)
    {
    }
}
