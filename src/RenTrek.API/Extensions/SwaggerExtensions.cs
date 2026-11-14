using System.Text.RegularExpressions;
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
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "renTrek API",
                Version = "v1",
                Description = """
                    REST API cho web renTrek (thuê đồ dã ngoại, đặt chuyến trekking).

                    **Cách thử API cần đăng nhập:** gọi `POST /api/v1/auth/login`, copy `data.accessToken`, bấm **Authorize** và dán token vào (không cần tiền tố `Bearer`).

                    Mọi response có dạng `{ success, statusCode, message, data, errors, timestamp }`.
                    """
            });
            options.CustomSchemaIds(GetSchemaId);

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

    // Phân biệt DTO cùng tên ở các namespace, kể cả bên trong ApiResponse<T> và List<T>.
    private static string GetSchemaId(Type type)
    {
        if (type.IsArray)
            return $"{GetSchemaId(type.GetElementType()!)}Array";

        if (!type.IsConstructedGenericType)
            return (type.FullName ?? type.Name).Replace('+', '.');

        var name = type.GetGenericTypeDefinition().FullName ?? type.Name;
        name = Regex.Replace(name.Replace('+', '.'), @"`\d+", string.Empty);
        return $"{name}_{string.Join("_", type.GetGenericArguments().Select(GetSchemaId))}";
    }
}
