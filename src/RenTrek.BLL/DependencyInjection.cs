using FluentValidation;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Implementations.Authentication;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Interfaces.Authentication;
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

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // Auth
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRegisterService, RegisterService>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ILogoutService, LogoutService>();

        // User & Profile
        services.AddScoped<IUserService, UserService>();

        // Trip booking
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RenTrek.BLL.Abstractions.IParticipantInviteTokenService, RenTrek.BLL.Security.ParticipantInviteTokenService>();
        services.AddScoped<ITripBookingService, TripBookingService>();

        return services;
    }
}
