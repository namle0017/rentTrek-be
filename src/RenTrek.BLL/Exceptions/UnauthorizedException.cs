namespace RenTrek.BLL.Exceptions;

public sealed class UnauthorizedException(string message) : AppException(message, 401);
