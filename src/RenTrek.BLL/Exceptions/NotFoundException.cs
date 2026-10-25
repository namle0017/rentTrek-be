namespace RenTrek.BLL.Exceptions;

public sealed class NotFoundException(string message) : AppException(message, 404);
