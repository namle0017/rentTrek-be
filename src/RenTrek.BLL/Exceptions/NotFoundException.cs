namespace RenTrek.BLL.Exceptions;

public class NotFoundException : AppException
{
    public NotFoundException(string message = "Không tìm thấy dữ liệu") : base(message, 404)
    {
    }
}
