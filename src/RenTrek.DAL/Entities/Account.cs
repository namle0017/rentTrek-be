namespace RenTrek.DAL.Entities;

// Bảng accounts
/// <summary>
/// Biểu diễn thực thể account trong mô hình dữ liệu RenTrek.
/// </summary>
public class Account
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public Guid RoleId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EmailConfirmedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsDeleted { get; set; }

    public Role Role { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<AccessToken> AccessTokens { get; set; } = new List<AccessToken>();
    public ICollection<ActivityAttendance> ActivityAttendances { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<BookingParticipant> BookingParticipants { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = [];
    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
    public Profile? Profile { get; set; }
    public ICollection<Promotion> Promotions { get; set; } = [];
    public ICollection<RefundPolicy> RefundPolicies { get; set; } = [];
    public ICollection<RentalCharge> RentalCharges { get; set; } = [];
    public ICollection<RentalOrder> RentalOrders { get; set; } = [];
    public ICollection<SystemConfiguration> SystemConfigurations { get; set; } = [];
    public ICollection<Tour> Tours { get; set; } = [];
    public ICollection<TripBookingOrder> TripBookingOrders { get; set; } = [];
    public ICollection<TripGroup> TripGroups { get; set; } = [];
    public ICollection<TripProvidedItem> TripProvidedItems { get; set; } = [];
    public ICollection<Trip> Trips { get; set; } = [];
    public ICollection<UserExternalLogin> UserExternalLogins { get; set; } = [];
}
