using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.API.Extensions;
using RenTrek.API.Filters;
using RenTrek.API.Middlewares;
using RenTrek.API.Services;
using RenTrek.BLL;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL;

const string CorsPolicyName = "DefaultCors";

var builder = WebApplication.CreateBuilder(args);

// ── Layers ──────────────────────────────────────────────
builder.Services.AddDataAccessLayer(builder.Configuration);
builder.Services.AddBusinessLayer(builder.Configuration);

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
builder.Services.AddHealthChecks();

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
app.MapHealthChecks("/health");

app.Run();
