namespace RenTrek.BLL.Exceptions;

public class ConflictException : AppException
{
    public ConflictException(string message = "Dữ liệu đã tồn tại") : base(message, 409)
    {
    }
}
