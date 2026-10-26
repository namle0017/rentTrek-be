using Microsoft.OpenApi.Models;

namespace RenTrek.API.Extensions;

/// <summary>
/// Đăng ký Swagger/OpenAPI kèm hỗ trợ JWT Bearer.
/// </summary>
public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "renTrek API", Version = "v1" });

            var bearerScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Nhập access token (không cần tiền tố 'Bearer ')",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };

            options.AddSecurityDefinition("Bearer", bearerScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearerScheme] = [] });
        });

        return services;
    }
}
