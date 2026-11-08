namespace RenTrek.BLL.Exceptions;

/// <summary>A required service is temporarily unavailable or not configured.</summary>
public sealed class ServiceUnavailableException(string message, string? errorCode = null)
    : AppException(message, 503, errorCode);
