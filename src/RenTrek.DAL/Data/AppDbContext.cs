using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Auth DbSets
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<TripBookingOrder> TripBookingOrders => Set<TripBookingOrder>();
    public DbSet<BookingParticipant> BookingParticipants => Set<BookingParticipant>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ParticipantInvitation> ParticipantInvitations => Set<ParticipantInvitation>();

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
