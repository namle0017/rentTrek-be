using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;

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
        services.Configure<SePaySettings>(configuration.GetSection(SePaySettings.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<InviteSettings>(configuration.GetSection(InviteSettings.SectionName));
        services.Configure<RefundSettings>(options =>
            options.DataEncryptionKey = configuration["RefundDataEncryptionKey"] ?? string.Empty);

        // Helpers
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtHelper, JwtHelper>();
        services.AddSingleton<IInviteTokenHelper, InviteTokenHelper>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IRefundDataProtector, RefundDataProtector>();

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITourService, TourService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ITripBookingService, TripBookingService>();
        services.AddScoped<IRefundCaseService, RefundCaseService>();
        services.AddScoped<IRefundVerificationService, RefundVerificationService>();
        services.AddScoped<ITripGroupService, TripGroupService>();
        services.AddScoped<IRentalAvailabilityService, RentalAvailabilityService>();
        services.AddScoped<IProductBundleService, ProductBundleService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<IRentalOrderService, RentalOrderService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPostService, PostService>();

        return services;
    }
}
