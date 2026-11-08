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
        services.AddAutoMapper(cfg =>
        {
            var licenseKey = configuration["AutoMapper:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                cfg.LicenseKey = licenseKey;
            }
        }, assembly);
        services.AddValidatorsFromAssembly(assembly);

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<SePaySettings>(configuration.GetSection(SePaySettings.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<InviteSettings>(configuration.GetSection(InviteSettings.SectionName));
        services.Configure<CloudinarySettings>(configuration.GetSection(CloudinarySettings.SectionName));
        services.AddMemoryCache();

        services.AddOptions<RefundSettings>()
            .Bind(configuration.GetSection("Refund"))
            .Configure(options => options.DataEncryptionKey = configuration["RefundDataEncryptionKey"] ?? string.Empty)
            .Validate(options => options.HasValidEncryptionKey(), "RefundDataEncryptionKey phải là chuỗi Base64 của 32 byte")
            .Validate(options => options.PayoutRetentionDays is >= 1 and <= 3650, "Refund:PayoutRetentionDays phải từ 1 đến 3650")
            .ValidateOnStart();

        // Helpers
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtHelper, JwtHelper>();
        services.AddSingleton<IInviteTokenHelper, InviteTokenHelper>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IRefundDataProtector, RefundDataProtector>();

        // Services
        // Phiên đăng nhập luôn theo giờ thật: tua đồng hồ giả lập không làm hết hạn token
        services.AddScoped<IAuthService>(sp => ActivatorUtilities.CreateInstance<AuthService>(sp, TimeProvider.System));
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<RenTrek.BLL.Services.Admin.IAdminService, RenTrek.BLL.Services.Admin.AdminService>();
        services.AddScoped<RenTrek.BLL.Services.Admin.IAdminSessionService, RenTrek.BLL.Services.Admin.AdminSessionService>();
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
        services.AddScoped<IRentalFulfillmentService, RentalFulfillmentService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<ITripProvidedItemService, TripProvidedItemService>();
        services.AddScoped<IFulfillmentService, FulfillmentService>();
        services.AddScoped<IAdminRecurrenceRuleService, AdminRecurrenceRuleService>();
        services.AddScoped<IAdminTripService, AdminTripService>();
        services.AddScoped<IAdminTripGroupService, AdminTripGroupService>();
        services.AddScoped<IAdminTripAttendanceService, AdminTripAttendanceService>();
        services.AddScoped<IGuidePortalService, GuidePortalService>();

        return services;
    }
}
