using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using RenTrek.API.Common;
using RenTrek.API.Middleware;
using RenTrek.API.Services;
using RenTrek.BLL;
using RenTrek.BLL.Abstractions;
using RenTrek.DAL;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(
                item => item.Key,
                item => item.Value!.Errors.Select(error =>
                    string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Dữ liệu không hợp lệ"
                        : error.ErrorMessage).ToArray());
        return new BadRequestObjectResult(
            ApiResponse<object>.Fail("Dữ liệu không hợp lệ", StatusCodes.Status400BadRequest, errors));
    };
});

builder.Services.AddDataAccessLayer(builder.Configuration);
builder.Services.AddBusinessLayer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var configuredJwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtConfigurationComplete = !string.IsNullOrWhiteSpace(configuredJwtKey)
    && !string.IsNullOrWhiteSpace(jwtIssuer)
    && !string.IsNullOrWhiteSpace(jwtAudience);
var allowsTemporaryJwtConfiguration = builder.Environment.IsDevelopment()
    || builder.Environment.IsEnvironment("Test");
if (!jwtConfigurationComplete && !allowsTemporaryJwtConfiguration)
    throw new InvalidOperationException("Jwt:Key, Jwt:Issuer, and Jwt:Audience are required outside Development/Test");
if (!string.IsNullOrWhiteSpace(configuredJwtKey) && Encoding.UTF8.GetByteCount(configuredJwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key must contain at least 32 bytes");

var jwtKey = string.IsNullOrWhiteSpace(configuredJwtKey)
    ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    : configuredJwtKey;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer),
            ValidIssuer = jwtIssuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (!jwtConfigurationComplete)
    app.Logger.LogWarning("JWT configuration is incomplete. Development/Test uses a temporary local-only configuration.");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
