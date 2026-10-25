using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RenTrek.API.Common;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.API.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt");
        var secretKey = jwt["SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey chưa được cấu hình hoặc ngắn hơn 32 ký tự (dùng dotnet user-secrets).");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwt["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Trả 401 / 403 theo format ApiResponse thay vì body rỗng
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
                                  ?? context.Principal?.FindFirst("jti")?.Value;

                        if (!string.IsNullOrEmpty(jti))
                        {
                            var unitOfWork = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
                            var isRevoked = await unitOfWork.AccessTokens.IsJtiRevokedAsync(jti);

                            if (isRevoked)
                            {
                                context.Fail("Phiên đăng nhập đã bị thu hồi hoặc đã đăng xuất.");
                            }
                        }
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                            "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn", StatusCodes.Status401Unauthorized));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                            "Bạn không có quyền thực hiện thao tác này", StatusCodes.Status403Forbidden));
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}
