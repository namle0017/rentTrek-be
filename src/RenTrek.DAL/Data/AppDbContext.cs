using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data;

/// <summary>
/// Quản lý phiên làm việc EF Core và các tập thực thể của RenTrek.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Auth DbSets
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();
    public DbSet<ActivityAttendance> ActivityAttendances => Set<ActivityAttendance>();
    public DbSet<AppliedPromotion> AppliedPromotions => Set<AppliedPromotion>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Product DbSets
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantImage> ProductVariantImages => Set<ProductVariantImage>();
    public DbSet<RentalPriceTier> RentalPriceTiers => Set<RentalPriceTier>();
    public DbSet<EquipmentAsset> EquipmentAssets => Set<EquipmentAsset>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ParticipantInvitation> ParticipantInvitations => Set<ParticipantInvitation>();
    public DbSet<ParticipantPostRead> ParticipantPostReads => Set<ParticipantPostRead>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostSoftSkill> PostSoftSkills => Set<PostSoftSkill>();
    public DbSet<PostTag> PostTags => Set<PostTag>();
    public DbSet<ProductBundle> ProductBundles => Set<ProductBundle>();
    public DbSet<ProductBundleItem> ProductBundleItems => Set<ProductBundleItem>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionTier> PromotionTiers => Set<PromotionTier>();
    public DbSet<RefundActionChallenge> RefundActionChallenges => Set<RefundActionChallenge>();
    public DbSet<RefundCase> RefundCases => Set<RefundCase>();
    public DbSet<RefundPolicy> RefundPolicies => Set<RefundPolicy>();
    public DbSet<RefundPolicyRule> RefundPolicyRules => Set<RefundPolicyRule>();
    public DbSet<RentalBundleItemSnapshot> RentalBundleItemSnapshots => Set<RentalBundleItemSnapshot>();
    public DbSet<RentalCharge> RentalCharges => Set<RentalCharge>();
    public DbSet<RentalOrder> RentalOrders => Set<RentalOrder>();
    public DbSet<RentalOrderItem> RentalOrderItems => Set<RentalOrderItem>();
    public DbSet<RentalOrderAdjustment> RentalOrderAdjustments => Set<RentalOrderAdjustment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<SoftSkill> SoftSkills => Set<SoftSkill>();
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TourPost> TourPosts => Set<TourPost>();
    public DbSet<TourRecurrenceRule> TourRecurrenceRules => Set<TourRecurrenceRule>();
    public DbSet<TripGroup> TripGroups => Set<TripGroup>();
    public DbSet<TripProvidedItem> TripProvidedItems => Set<TripProvidedItem>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();

    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<TripBookingOrder> TripBookingOrders => Set<TripBookingOrder>();
    public DbSet<BookingParticipant> BookingParticipants => Set<BookingParticipant>();
    public DbSet<Payment> Payments => Set<Payment>();

    // Tour DbSets
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourImage> TourImages => Set<TourImage>();
    public DbSet<TourActivity> TourActivities => Set<TourActivity>();
    public DbSet<TourPreparationItem> TourPreparationItems => Set<TourPreparationItem>();
    public DbSet<TourProvidedItem> TourProvidedItems => Set<TourProvidedItem>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Location> Locations => Set<Location>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tự động nạp mọi IEntityTypeConfiguration<T> trong Data/Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Supabase dùng snake_case: chuyển tên bảng/cột chưa đặt theo quy ước
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            entityType.SetTableName(ToSnakeCase(entityType.GetTableName()!));
            foreach (var property in entityType.GetProperties())
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
        }

        // Global query filter tự động cho Soft Delete (kế thừa BaseEntity)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var falseConstant = Expression.Constant(false);
                var filter = Expression.Lambda(Expression.Equal(property, falseConstant), parameter);

                entityType.SetQueryFilter(filter);
            }
        }
    }

    private static string ToSnakeCase(string name)
        => Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Deleted)
            {
                // Soft delete
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
