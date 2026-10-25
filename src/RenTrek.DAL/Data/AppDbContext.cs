using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tự động nạp mọi IEntityTypeConfiguration<T> trong Data/Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
