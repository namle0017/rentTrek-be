using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Implementations.Authentication;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Interfaces.Authentication;

namespace RenTrek.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // AutoMapper
        services.AddAutoMapper(cfg => cfg.LicenseKey = configuration["AutoMapper:LicenseKey"], assembly);
        services.AddValidatorsFromAssembly(assembly);

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // Auth Services
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRegisterService, RegisterService>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ILogoutService, LogoutService>();
        services.AddScoped<IProductService, ProductService>();

        // User & Profile
        services.AddScoped<IUserService, UserService>();

        // Trip booking
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RenTrek.BLL.Abstractions.IParticipantInviteTokenService, RenTrek.BLL.Security.ParticipantInviteTokenService>();
        services.AddScoped<ITripBookingService, TripBookingService>();

        // Tour Services
        services.AddScoped<ITourService, TourService>();

        return services;
    }
}
