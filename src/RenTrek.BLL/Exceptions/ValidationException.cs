namespace RenTrek.BLL.Exceptions;

public class ValidationException : AppException
{
    /// <summary>Lỗi theo từng field: key là tên field (camelCase), value là danh sách message.</summary>
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors, string message = "Dữ liệu không hợp lệ")
        : base(message, 400)
    {
        Errors = errors;
    }
}
