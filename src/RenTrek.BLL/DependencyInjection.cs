using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RenTrek.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // AutoMapper 15+ cần license key (miễn phí cho mục đích học tập / phi thương mại)
        services.AddAutoMapper(cfg => cfg.LicenseKey = configuration["AutoMapper:LicenseKey"], assembly);
        services.AddValidatorsFromAssembly(assembly);

        // Service đăng ký tại đây, ví dụ:
        // services.AddScoped<IUserService, UserService>();

        return services;
    }
}
