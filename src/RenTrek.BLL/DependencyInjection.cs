using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Internal;

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
        services.Configure<SupabaseStorageSettings>(configuration.GetSection(SupabaseStorageSettings.SectionName));
        services.Configure<CloudinarySettings>(configuration.GetSection(CloudinarySettings.SectionName));
        services.AddScoped<IProductImageUploadService, ProductImageUploadService>();
        services.Configure<GoogleAuthSettings>(configuration.GetSection(GoogleAuthSettings.SectionName));
        services.AddOptions<RefundSettings>()
            .Bind(configuration.GetSection("Refund"))
            .Configure(options => options.DataEncryptionKey = configuration["RefundDataEncryptionKey"] ?? string.Empty)
            .Validate(options => options.HasValidEncryptionKey(), "RefundDataEncryptionKey phải là chuỗi Base64 của 32 byte")
            .Validate(options => options.PayoutRetentionDays is >= 1 and <= 3650, "Refund:PayoutRetentionDays phải từ 1 đến 3650")
            .ValidateOnStart();

        // Helpers
        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtHelper, JwtHelper>();
        services.AddSingleton<IInviteTokenHelper, InviteTokenHelper>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IRefundDataProtector, RefundDataProtector>();
        services.AddScoped<IAuthAttemptLimiter, AuthAttemptLimiter>();

        // Services
        // Phiên đăng nhập luôn theo giờ thật: tua đồng hồ giả lập không làm hết hạn token
        services.AddScoped<IAuthService>(sp => ActivatorUtilities.CreateInstance<AuthService>(sp, TimeProvider.System));
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddSingleton<IPrivateFileStorage, PrivateFileStorage>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminMasterDataService, AdminMasterDataService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();
        services.AddScoped<IAdminPromotionService, AdminPromotionService>();
        services.AddScoped<IAdminRefundPolicyService, AdminRefundPolicyService>();
        services.AddScoped<IAdminPaymentService, AdminPaymentService>();
        services.AddScoped<IAdminProductCatalogService, AdminProductCatalogService>();
        services.AddScoped<IAdminTourService, AdminTourService>();
        services.AddScoped<IAdminGovernanceService, AdminGovernanceService>();
        services.AddScoped<IAdminReportService, AdminReportService>();
        // Kiểm tra phiên cũng theo giờ thật, cùng đồng hồ với AuthService lúc cấp token
        services.AddScoped<IAdminSessionService>(sp => ActivatorUtilities.CreateInstance<AdminSessionService>(sp, TimeProvider.System));
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITourService, TourService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ITripBookingService, TripBookingService>();
        services.AddScoped<TripRescheduleService>();
        services.AddScoped<ITripRescheduleService>(sp => sp.GetRequiredService<TripRescheduleService>());
        services.AddScoped<ITripReschedulePaymentHandler>(sp => sp.GetRequiredService<TripRescheduleService>());
        services.AddScoped<RefundCaseService>();
        services.AddScoped<IRefundCaseService>(sp => sp.GetRequiredService<RefundCaseService>());
        services.AddScoped<IRentalRefundOpener>(sp => sp.GetRequiredService<RefundCaseService>());
        services.AddScoped<IRefundVerificationService, RefundVerificationService>();
        services.AddScoped<ITripRecurrenceService, TripRecurrenceService>();
        services.AddScoped<ITripService, TripService>();
        services.AddScoped<ITripGroupingService, TripGroupingService>();
        services.AddScoped<ITripGroupPublishService, TripGroupPublishService>();
        services.AddScoped<ITripGroupViewService, TripGroupViewService>();
        services.AddScoped<IRentalAvailabilityService, RentalAvailabilityService>();
        services.AddScoped<IProductBundleService, ProductBundleService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<RentalOrderService>();
        services.AddScoped<IRentalOrderService>(sp => sp.GetRequiredService<RentalOrderService>());
        services.AddScoped<IRentalExtensionApplier>(sp => sp.GetRequiredService<RentalOrderService>());
        services.AddScoped<IRentalPolicyService, RentalPolicyService>();
        services.AddScoped<IRentalNoticeDispatcher, RentalNoticeDispatcher>();
        services.AddScoped<IRentalCancellationService, RentalCancellationService>();
        services.AddScoped<IRentalLifecycleService, RentalLifecycleService>();
        services.AddScoped<ITripBookingExpiryService, TripBookingExpiryService>();
        services.AddScoped<IRentalEvidenceService, RentalEvidenceService>();
        services.AddScoped<IRentalReportService, RentalReportService>();
        services.AddScoped<ICustomerReportService, CustomerReportService>();
        // Một instance cho cả quyết toán và xử lý khoản thu sau (webhook) trong cùng scope
        services.AddScoped<RentalSettlementService>();
        services.AddScoped<IRentalSettlementService>(sp => sp.GetRequiredService<RentalSettlementService>());
        services.AddScoped<IRentalPostPaymentService>(sp => sp.GetRequiredService<RentalSettlementService>());
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IParticipationService, ParticipationService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IRentalFulfillmentService, RentalFulfillmentService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<ITripProvidedItemService, TripProvidedItemService>();
        services.AddScoped<IAdminRecurrenceRuleService, AdminRecurrenceRuleService>();
        services.AddScoped<IAdminTripService, AdminTripService>();
        services.AddScoped<IAdminTripGroupService, AdminTripGroupService>();
        services.AddScoped<IAdminTripAttendanceService, AdminTripAttendanceService>();
        services.AddScoped<IGuidePortalService, GuidePortalService>();

        return services;
    }
}
