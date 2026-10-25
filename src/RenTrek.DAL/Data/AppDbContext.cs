using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ActivityAttendance> ActivityAttendances => Set<ActivityAttendance>();
    public DbSet<AppliedPromotion> AppliedPromotions => Set<AppliedPromotion>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BookingParticipant> BookingParticipants => Set<BookingParticipant>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EquipmentAsset> EquipmentAssets => Set<EquipmentAsset>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<ParticipantPostRead> ParticipantPostReads => Set<ParticipantPostRead>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostSoftSkill> PostSoftSkills => Set<PostSoftSkill>();
    public DbSet<PostTag> PostTags => Set<PostTag>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBundle> ProductBundles => Set<ProductBundle>();
    public DbSet<ProductBundleItem> ProductBundleItems => Set<ProductBundleItem>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantImage> ProductVariantImages => Set<ProductVariantImage>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionTier> PromotionTiers => Set<PromotionTier>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RefundPolicy> RefundPolicies => Set<RefundPolicy>();
    public DbSet<RefundPolicyRule> RefundPolicyRules => Set<RefundPolicyRule>();
    public DbSet<RentalBundleItemSnapshot> RentalBundleItemSnapshots => Set<RentalBundleItemSnapshot>();
    public DbSet<RentalCharge> RentalCharges => Set<RentalCharge>();
    public DbSet<RentalOrder> RentalOrders => Set<RentalOrder>();
    public DbSet<RentalOrderItem> RentalOrderItems => Set<RentalOrderItem>();
    public DbSet<RentalPriceTier> RentalPriceTiers => Set<RentalPriceTier>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<SoftSkill> SoftSkills => Set<SoftSkill>();
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourActivity> TourActivities => Set<TourActivity>();
    public DbSet<TourImage> TourImages => Set<TourImage>();
    public DbSet<TourPost> TourPosts => Set<TourPost>();
    public DbSet<TourPreparationItem> TourPreparationItems => Set<TourPreparationItem>();
    public DbSet<TourProvidedItem> TourProvidedItems => Set<TourProvidedItem>();
    public DbSet<TourRecurrenceRule> TourRecurrenceRules => Set<TourRecurrenceRule>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripBookingOrder> TripBookingOrders => Set<TripBookingOrder>();
    public DbSet<TripGroup> TripGroups => Set<TripGroup>();
    public DbSet<TripProvidedItem> TripProvidedItems => Set<TripProvidedItem>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Extension / enum của PostgreSQL
        modelBuilder
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone", "recovery_code" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in", "like", "ilike", "is", "match", "imatch", "isdistinct" })
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("vault", "supabase_vault");

        // Tự động nạp mọi IEntityTypeConfiguration<T> trong Data/Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
