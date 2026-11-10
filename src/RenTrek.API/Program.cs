using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DotNetEnv;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RenTrek.API.BackgroundServices;
using RenTrek.API.Common;
using RenTrek.API.Extensions;
using RenTrek.API.Filters;
using RenTrek.API.Middlewares;
using RenTrek.API.Identity;
using RenTrek.BLL;
using RenTrek.BLL.Constants;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL;

const string CorsPolicyName = "DefaultCors";

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// ── Layers ──────────────────────────────────────────────
builder.Services.AddDataAccessLayer(builder.Configuration);
builder.Services.AddBusinessLayer(builder.Configuration);

// Development: đồng hồ giả lập để test luồng theo thời gian (trước / trong / sau chuyến, trả đồ trễ).
// Điều khiển qua /api/v1/dev/clock; môi trường khác vẫn dùng TimeProvider.System.
if (builder.Environment.IsDevelopment())
{
    var simulatedClock = new SimulatedTimeProvider();
    builder.Services.AddSingleton(simulatedClock);
    builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(simulatedClock));
}

// ── Presentation ────────────────────────────────────────
builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Lỗi model binding (sai kiểu dữ liệu, JSON hỏng...) trả về theo format ApiResponse
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => e.Key,
                e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());

        return new BadRequestObjectResult(
            ApiResponse<object>.Fail("Dữ liệu không hợp lệ", StatusCodes.Status400BadRequest, errors));
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerWithJwt();

// Chỉ cho phép domain FE khai báo trong Cors__AllowedOrigins__N (.env / Render)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicyName, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Giới hạn theo tài khoản cho thao tác hoàn tiền (§11), đọc user nên phải đứng sau UseAuthentication
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancellationToken) => new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
        ApiResponse<object>.Fail(
            "Bạn thao tác quá nhanh, vui lòng thử lại sau",
            StatusCodes.Status429TooManyRequests,
            new Dictionary<string, string[]> { ["code"] = [ErrorCodes.TooManyRequests] }),
        cancellationToken));
    options.AddPolicy(RateLimitPolicies.RefundSensitive, context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy(RateLimitPolicies.AuthSensitive, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

// Render đứng sau proxy: lấy IP client thật từ X-Forwarded-For (phần tử cuối do proxy gắn) cho rate limit theo IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHostedService<RefundDataPurgeBackgroundService>();

// Render gọi /health để biết container đã sẵn sàng (liveness, không gọi DB)
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Pipeline (thứ tự theo docs/01-CONVENTIONS.md §2.1) ──
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseForwardedHeaders();

// Bật cả trên Render để team thử API tại /swagger
app.UseSwagger();
app.UseSwaggerUI(options => options.DocumentTitle = "renTrek API");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors(CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
