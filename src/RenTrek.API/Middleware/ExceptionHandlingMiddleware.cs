using RenTrek.API.Common;
using RenTrek.BLL.Exceptions;

namespace RenTrek.API.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var (statusCode, message) = exception switch
            {
                AppException appException => (appException.StatusCode, appException.Message),
                _ => (StatusCodes.Status500InternalServerError, "Đã xảy ra lỗi hệ thống")
            };

            if (statusCode >= StatusCodes.Status500InternalServerError)
                logger.LogError(exception, "Unhandled exception");

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail(message, statusCode),
                context.RequestAborted);
        }
    }
}
