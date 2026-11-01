using RenTrek.API.Common;
using RenTrek.BLL.Exceptions;

namespace RenTrek.API.Middlewares;

/// <summary>
/// Bắt mọi exception trong pipeline và trả về ApiResponse lỗi chuẩn. Luôn đăng ký đầu tiên trong Program.cs.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var response = ex switch
        {
            AppException appEx =>
                ApiResponse<object>.Fail(appEx.Message, appEx.StatusCode, BuildErrors(appEx)),
            _ =>
                ApiResponse<object>.Fail(
                    _environment.IsDevelopment() ? ex.Message : "Đã xảy ra lỗi hệ thống",
                    StatusCodes.Status500InternalServerError)
        };

        if (response.StatusCode >= StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Unhandled exception at {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);

        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = response.StatusCode;
        await context.Response.WriteAsJsonAsync(response);
    }

    /// <summary>
    /// Gộp lỗi theo field (ValidationException / ConflictException) với mã lỗi nghiệp vụ (errors["code"] = [ErrorCode]).
    /// Trả null khi không có gì để gộp.
    /// </summary>
    private static IDictionary<string, string[]>? BuildErrors(AppException appEx)
    {
        var source = appEx switch
        {
            ValidationException validationEx => validationEx.Errors,
            ConflictException conflictEx => conflictEx.Errors,
            _ => null
        };

        if (source is null && appEx.ErrorCode is null)
            return null;

        var errors = source is null
            ? new Dictionary<string, string[]>()
            : new Dictionary<string, string[]>(source);

        if (appEx.ErrorCode is not null)
            errors["code"] = [appEx.ErrorCode];

        return errors;
    }
}
