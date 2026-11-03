using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Warehouse.Implementations;
using RenTrek.BLL.Services.Warehouse.Interfaces;

namespace RenTrek.BLL;

/// <summary>
/// Đăng ký các dịch vụ business của BLL vào dependency injection container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // AutoMapper
        services.AddAutoMapper(cfg => cfg.LicenseKey = configuration["AutoMapper:LicenseKey"], assembly);
        services.AddValidatorsFromAssembly(assembly);

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // Helpers
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtHelper, JwtHelper>();
        services.AddSingleton<IInviteTokenHelper, InviteTokenHelper>();

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITourService, TourService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ITripBookingService, TripBookingService>();
        services.AddScoped<IRentalAvailabilityService, RentalAvailabilityService>();
        services.AddScoped<IProductBundleService, ProductBundleService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<IRentalOrderService, RentalOrderService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAssetService, AssetService>();

        return services;
    }
}
