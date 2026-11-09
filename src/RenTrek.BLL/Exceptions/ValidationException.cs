namespace RenTrek.BLL.Exceptions;

/// <summary>
/// Biểu diễn lỗi validation trong tầng business.
/// </summary>
public class ValidationException : AppException
{
    /// <summary>Lỗi theo từng field: key là tên field (camelCase), value là danh sách message.</summary>
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(
        IDictionary<string, string[]> errors,
        string message = "Dữ liệu không hợp lệ",
        string? errorCode = null)
        : base(message, 400, errorCode)
    {
        Errors = errors;
    }

    public ValidationException(string propertyName, string errorMessage)
        : base("Dữ liệu không hợp lệ", 400)
    {
        Errors = new Dictionary<string, string[]>
        {
            [propertyName] = [errorMessage]
        };
    }

    public ValidationException(string errorMessage)
        : base(errorMessage, 400)
    {
        Errors = new Dictionary<string, string[]>
        {
            ["general"] = [errorMessage]
        };
    }
}
