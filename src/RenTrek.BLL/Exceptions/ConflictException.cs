namespace RenTrek.BLL.Exceptions;

public sealed class ConflictException(string message) : AppException(message, 409);
