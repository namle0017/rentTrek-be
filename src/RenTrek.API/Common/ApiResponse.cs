namespace RenTrek.API.Common;

/// <summary>
/// Chuẩn hóa cấu trúc phản hồi thành công và lỗi của API.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string message = "Success", int statusCode = StatusCodes.Status200OK)
        => new() { Success = true, StatusCode = statusCode, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, int statusCode, IDictionary<string, string[]>? errors = null)
        => new() { Success = false, StatusCode = statusCode, Message = message, Errors = errors };
}
