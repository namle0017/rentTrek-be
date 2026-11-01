using System.Text.Json.Serialization;
using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.API.Extensions;
using RenTrek.API.Filters;
using RenTrek.API.Middlewares;
using RenTrek.API.Identity;
using RenTrek.BLL;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL;
using Microsoft.AspNetCore.DataProtection;

const string CorsPolicyName = "DefaultCors";

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// ── Layers ──────────────────────────────────────────────
builder.Services.AddDataAccessLayer(builder.Configuration);
builder.Services.AddBusinessLayer(builder.Configuration);
var paymentKeyRing = builder.Configuration["Payments:KeyRingPath"];
if (string.IsNullOrWhiteSpace(paymentKeyRing) && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Payments:KeyRingPath must point to a durable shared key directory.");
if (string.IsNullOrWhiteSpace(paymentKeyRing)) paymentKeyRing = Path.Combine(builder.Environment.ContentRootPath, ".payment-keys");
builder.Services.AddDataProtection().SetApplicationName("RenTrek.Payments").PersistKeysToFileSystem(new DirectoryInfo(paymentKeyRing));
builder.Services.AddScoped<IPaymentDataProtector, PaymentDataProtector>();
builder.Services.AddSingleton<RenTrek.API.BackgroundJobs.PaymentMaintenanceRunner>();
builder.Services.AddHostedService<RenTrek.API.BackgroundJobs.PaymentMaintenanceWorker>();

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

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicyName, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()));

// Render gọi /health để biết container đã sẵn sàng (liveness, không gọi DB)
builder.Services.AddHealthChecks().AddCheck<RenTrek.API.Health.PaymentReadinessHealthCheck>("payment-readiness", tags: ["ready"]);

var app = builder.Build();

// ── Pipeline (thứ tự theo docs/01-CONVENTIONS.md §2.1) ──
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

public partial class Program;
