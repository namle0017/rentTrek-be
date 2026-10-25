namespace RenTrek.DAL.Entities;

public class Account
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public string Email { get; set; } = null!;
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? EmailConfirmedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<AccessToken> AccessTokens { get; set; } = new List<AccessToken>();
    public ICollection<ActivityAttendance> ActivityAttendances { get; set; } = new List<ActivityAttendance>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<BookingParticipant> BookingParticipants { get; set; } = new List<BookingParticipant>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public Profile? Profile { get; set; }
    public ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<RefundPolicy> RefundPolicies { get; set; } = new List<RefundPolicy>();
    public ICollection<RentalCharge> RentalCharges { get; set; } = new List<RentalCharge>();
    public ICollection<RentalOrder> RentalOrders { get; set; } = new List<RentalOrder>();
    public Role Role { get; set; } = null!;
    public ICollection<SystemConfiguration> SystemConfigurations { get; set; } = new List<SystemConfiguration>();
    public ICollection<Tour> Tours { get; set; } = new List<Tour>();
    public ICollection<TripBookingOrder> TripBookingOrders { get; set; } = new List<TripBookingOrder>();
    public ICollection<TripGroup> TripGroups { get; set; } = new List<TripGroup>();
    public ICollection<TripProvidedItem> TripProvidedItems { get; set; } = new List<TripProvidedItem>();
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
    public ICollection<UserExternalLogin> UserExternalLogins { get; set; } = new List<UserExternalLogin>();
}
