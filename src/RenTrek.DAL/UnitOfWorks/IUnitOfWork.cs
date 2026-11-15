using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.UnitOfWorks;

public interface IUnitOfWork : IDisposable
{
    IAdminOperationRepository AdminOperations { get; }
    // Mọi thao tác DB của Service đi qua các repository dưới đây (dùng chung DbContext / transaction của UnitOfWork)
    IAccessTokenRepository AccessTokens { get; }
    IAccountRepository Accounts { get; }
    IActivityAttendanceRepository ActivityAttendances { get; }
    IAppliedPromotionRepository AppliedPromotions { get; }
    IAssetAssignmentRepository AssetAssignments { get; }
    IAuditLogRepository AuditLogs { get; }
    IBookingParticipantRepository BookingParticipants { get; }
    ICategoryRepository Categories { get; }
    IEquipmentAssetRepository EquipmentAssets { get; }
    ILocationRepository Locations { get; }
    INotificationRepository Notifications { get; }
    IOrderRepository Orders { get; }
    IParticipantPostReadRepository ParticipantPostReads { get; }
    IPasswordResetTokenRepository PasswordResetTokens { get; }
    IPaymentRepository Payments { get; }
    IPostRepository Posts { get; }
    IPostSoftSkillRepository PostSoftSkills { get; }
    IPostTagRepository PostTags { get; }
    IProductRepository Products { get; }
    IProductBundleRepository ProductBundles { get; }
    IProductBundleItemRepository ProductBundleItems { get; }
    IProductVariantRepository ProductVariants { get; }
    IProductVariantImageRepository ProductVariantImages { get; }
    IProfileRepository Profiles { get; }
    IPromotionRepository Promotions { get; }
    IPromotionTierRepository PromotionTiers { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IRefundCaseRepository RefundCases { get; }
    IRefundPolicyRepository RefundPolicies { get; }
    // Mặc định ném lỗi để các UnitOfWork giả trong test không cần cài đặt; UnitOfWork thật override
    IRefundProofRepository RefundProofs => throw new NotSupportedException();
    IRefundPolicyRuleRepository RefundPolicyRules { get; }
    IRentalBundleItemSnapshotRepository RentalBundleItemSnapshots { get; }
    IRentalChargeRepository RentalCharges { get; }
    IRentalOrderRepository RentalOrders { get; }
    IRentalOrderItemRepository RentalOrderItems { get; }
    IRentalPriceTierRepository RentalPriceTiers { get; }
    IRentalPolicyRepository RentalPolicies { get; }
    IRentalEvidencePhotoRepository RentalEvidencePhotos { get; }
    IRentalReportRepository RentalReports { get; }
    IRentalSettlementRepository RentalSettlements { get; }
    IReviewRepository Reviews { get; }
    IRoleRepository Roles { get; }
    ISoftSkillRepository SoftSkills { get; }
    ISystemConfigurationRepository SystemConfigurations { get; }
    ITagRepository Tags { get; }
    ITourRepository Tours { get; }
    ITourActivityRepository TourActivities { get; }
    ITourImageRepository TourImages { get; }
    ITourPostRepository TourPosts { get; }
    ITourPreparationItemRepository TourPreparationItems { get; }
    ITourProvidedItemRepository TourProvidedItems { get; }
    ITourRecurrenceRuleRepository TourRecurrenceRules { get; }
    ITripRepository Trips { get; }
    ITripBookingOrderRepository TripBookingOrders { get; }
    ITripBookingRepository TripBookings { get; }
    ITripGroupRepository TripGroups { get; }
    ITripProvidedItemRepository TripProvidedItems { get; }
    IUserExternalLoginRepository UserExternalLogins { get; }

    /// <summary>Lấy generic repository cho entity bất kỳ (ưu tiên dùng property riêng ở trên).</summary>
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu thay đổi; nếu PostgreSQL báo vi phạm ràng buộc duy nhất (23505) thì gỡ các thực thể lỗi khỏi ChangeTracker và trả về false.
    /// </summary>
    Task<bool> TrySaveChangesIgnoringUniqueViolationAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>Bỏ mọi thực thể đang tracking (dùng khi một đơn vị công việc độc lập thất bại giữa vòng lặp xử lý hàng loạt).</summary>
    void ClearChangeTracker() { }
}
