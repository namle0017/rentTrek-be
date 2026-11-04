using Microsoft.EntityFrameworkCore.Storage;
using RenTrek.DAL.Data;
using RenTrek.DAL.Repositories.Implementations;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.UnitOfWorks;

/// <summary>
/// Điều phối repository, lưu thay đổi và transaction trong một phạm vi nghiệp vụ.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    public IAdminOperationRepository AdminOperations => new AdminOperationRepository(_context);
    private readonly AppDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;

    private IAccessTokenRepository? _accessTokens;
    private IAccountRepository? _accounts;
    private IActivityAttendanceRepository? _activityAttendances;
    private IAppliedPromotionRepository? _appliedPromotions;
    private IAssetAssignmentRepository? _assetAssignments;
    private IAuditLogRepository? _auditLogs;
    private IBookingParticipantRepository? _bookingParticipants;
    private ICategoryRepository? _categories;
    private IEquipmentAssetRepository? _equipmentAssets;
    private ILocationRepository? _locations;
    private INotificationRepository? _notifications;
    private IOrderRepository? _orders;
    private IParticipantPostReadRepository? _participantPostReads;
    private IPasswordResetTokenRepository? _passwordResetTokens;
    private IPaymentRepository? _payments;
    private IPostRepository? _posts;
    private IPostSoftSkillRepository? _postSoftSkills;
    private IPostTagRepository? _postTags;
    private IProductRepository? _products;
    private IProductBundleRepository? _productBundles;
    private IProductBundleItemRepository? _productBundleItems;
    private IProductVariantRepository? _productVariants;
    private IProductVariantImageRepository? _productVariantImages;
    private IProfileRepository? _profiles;
    private IPromotionRepository? _promotions;
    private IPromotionTierRepository? _promotionTiers;
    private IRefreshTokenRepository? _refreshTokens;
    private IRefundCaseRepository? _refundCases;
    private IRefundPolicyRepository? _refundPolicies;
    private IRefundPolicyRuleRepository? _refundPolicyRules;
    private IRentalBundleItemSnapshotRepository? _rentalBundleItemSnapshots;
    private IRentalChargeRepository? _rentalCharges;
    private IRentalOrderRepository? _rentalOrders;
    private IRentalOrderItemRepository? _rentalOrderItems;
    private IRentalPriceTierRepository? _rentalPriceTiers;
    private IReviewRepository? _reviews;
    private IRoleRepository? _roles;
    private ISoftSkillRepository? _softSkills;
    private ISystemConfigurationRepository? _systemConfigurations;
    private ITagRepository? _tags;
    private ITourRepository? _tours;
    private ITourActivityRepository? _tourActivities;
    private ITourImageRepository? _tourImages;
    private ITourPostRepository? _tourPosts;
    private ITourPreparationItemRepository? _tourPreparationItems;
    private ITourProvidedItemRepository? _tourProvidedItems;
    private ITourRecurrenceRuleRepository? _tourRecurrenceRules;
    private ITripRepository? _trips;
    private ITripBookingOrderRepository? _tripBookingOrders;
    private ITripBookingRepository? _tripBookings;
    private ITripGroupRepository? _tripGroups;
    private ITripProvidedItemRepository? _tripProvidedItems;
    private IUserExternalLoginRepository? _userExternalLogins;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    // Repository tạo khi dùng lần đầu, dùng chung _context để SaveChanges / transaction áp dụng cho tất cả
    public IAccessTokenRepository AccessTokens => _accessTokens ??= new AccessTokenRepository(_context);
    public IAccountRepository Accounts => _accounts ??= new AccountRepository(_context);
    public IActivityAttendanceRepository ActivityAttendances => _activityAttendances ??= new ActivityAttendanceRepository(_context);
    public IAppliedPromotionRepository AppliedPromotions => _appliedPromotions ??= new AppliedPromotionRepository(_context);
    public IAssetAssignmentRepository AssetAssignments => _assetAssignments ??= new AssetAssignmentRepository(_context);
    public IAuditLogRepository AuditLogs => _auditLogs ??= new AuditLogRepository(_context);
    public IBookingParticipantRepository BookingParticipants => _bookingParticipants ??= new BookingParticipantRepository(_context);
    public ICategoryRepository Categories => _categories ??= new CategoryRepository(_context);
    public IEquipmentAssetRepository EquipmentAssets => _equipmentAssets ??= new EquipmentAssetRepository(_context);
    public ILocationRepository Locations => _locations ??= new LocationRepository(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public IOrderRepository Orders => _orders ??= new OrderRepository(_context);
    public IParticipantPostReadRepository ParticipantPostReads => _participantPostReads ??= new ParticipantPostReadRepository(_context);
    public IPasswordResetTokenRepository PasswordResetTokens => _passwordResetTokens ??= new PasswordResetTokenRepository(_context);
    public IPaymentRepository Payments => _payments ??= new PaymentRepository(_context);
    public IPostRepository Posts => _posts ??= new PostRepository(_context);
    public IPostSoftSkillRepository PostSoftSkills => _postSoftSkills ??= new PostSoftSkillRepository(_context);
    public IPostTagRepository PostTags => _postTags ??= new PostTagRepository(_context);
    public IProductRepository Products => _products ??= new ProductRepository(_context);
    public IProductBundleRepository ProductBundles => _productBundles ??= new ProductBundleRepository(_context);
    public IProductBundleItemRepository ProductBundleItems => _productBundleItems ??= new ProductBundleItemRepository(_context);
    public IProductVariantRepository ProductVariants => _productVariants ??= new ProductVariantRepository(_context);
    public IProductVariantImageRepository ProductVariantImages => _productVariantImages ??= new ProductVariantImageRepository(_context);
    public IProfileRepository Profiles => _profiles ??= new ProfileRepository(_context);
    public IPromotionRepository Promotions => _promotions ??= new PromotionRepository(_context);
    public IPromotionTierRepository PromotionTiers => _promotionTiers ??= new PromotionTierRepository(_context);
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_context);
    public IRefundCaseRepository RefundCases => _refundCases ??= new RefundCaseRepository(_context);
    public IRefundPolicyRepository RefundPolicies => _refundPolicies ??= new RefundPolicyRepository(_context);
    public IRefundPolicyRuleRepository RefundPolicyRules => _refundPolicyRules ??= new RefundPolicyRuleRepository(_context);
    public IRentalBundleItemSnapshotRepository RentalBundleItemSnapshots => _rentalBundleItemSnapshots ??= new RentalBundleItemSnapshotRepository(_context);
    public IRentalChargeRepository RentalCharges => _rentalCharges ??= new RentalChargeRepository(_context);
    public IRentalOrderRepository RentalOrders => _rentalOrders ??= new RentalOrderRepository(_context);
    public IRentalOrderItemRepository RentalOrderItems => _rentalOrderItems ??= new RentalOrderItemRepository(_context);
    public IRentalPriceTierRepository RentalPriceTiers => _rentalPriceTiers ??= new RentalPriceTierRepository(_context);
    public IReviewRepository Reviews => _reviews ??= new ReviewRepository(_context);
    public IRoleRepository Roles => _roles ??= new RoleRepository(_context);
    public ISoftSkillRepository SoftSkills => _softSkills ??= new SoftSkillRepository(_context);
    public ISystemConfigurationRepository SystemConfigurations => _systemConfigurations ??= new SystemConfigurationRepository(_context);
    public ITagRepository Tags => _tags ??= new TagRepository(_context);
    public ITourRepository Tours => _tours ??= new TourRepository(_context);
    public ITourActivityRepository TourActivities => _tourActivities ??= new TourActivityRepository(_context);
    public ITourImageRepository TourImages => _tourImages ??= new TourImageRepository(_context);
    public ITourPostRepository TourPosts => _tourPosts ??= new TourPostRepository(_context);
    public ITourPreparationItemRepository TourPreparationItems => _tourPreparationItems ??= new TourPreparationItemRepository(_context);
    public ITourProvidedItemRepository TourProvidedItems => _tourProvidedItems ??= new TourProvidedItemRepository(_context);
    public ITourRecurrenceRuleRepository TourRecurrenceRules => _tourRecurrenceRules ??= new TourRecurrenceRuleRepository(_context);
    public ITripRepository Trips => _trips ??= new TripRepository(_context);
    public ITripBookingOrderRepository TripBookingOrders => _tripBookingOrders ??= new TripBookingOrderRepository(_context);
    public ITripBookingRepository TripBookings => _tripBookings ??= new TripBookingRepository(_context);
    public ITripGroupRepository TripGroups => _tripGroups ??= new TripGroupRepository(_context);
    public ITripProvidedItemRepository TripProvidedItems => _tripProvidedItems ??= new TripProvidedItemRepository(_context);
    public IUserExternalLoginRepository UserExternalLogins => _userExternalLogins ??= new UserExternalLoginRepository(_context);

    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        if (!_repositories.TryGetValue(typeof(TEntity), out var repository))
        {
            repository = new GenericRepository<TEntity>(_context);
            _repositories[typeof(TEntity)] = repository;
        }

        return (IGenericRepository<TEntity>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction ??= await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("Chưa gọi BeginTransactionAsync.");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await _transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    // AppDbContext do DI container quản lý vòng đời, UnitOfWork chỉ dọn transaction của mình
    public void Dispose()
    {
        _transaction?.Dispose();
        _transaction = null;
        GC.SuppressFinalize(this);
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
