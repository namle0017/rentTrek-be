using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể trip booking reschedule.
/// </summary>
public sealed class TripBookingRescheduleConfiguration : IEntityTypeConfiguration<TripBookingReschedule>
{
    public void Configure(EntityTypeBuilder<TripBookingReschedule> builder)
    {
        builder.ToTable("trip_booking_reschedules");
        builder.HasKey(x => x.Id).HasName("trip_booking_reschedules_pkey");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.FromTripId).HasColumnName("from_trip_id");
        builder.Property(x => x.ToTripId).HasColumnName("to_trip_id");
        builder.Property(x => x.ParticipantCount).HasColumnName("participant_count");
        builder.Property(x => x.PriceDiff).HasColumnName("price_diff").HasPrecision(18, 0);
        builder.Property(x => x.Fee).HasColumnName("fee").HasPrecision(18, 0);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(x => x.PaymentId).HasColumnName("payment_id");
        builder.Property(x => x.HoldExpiresAt).HasColumnName("hold_expires_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.HasIndex(x => x.OrderId, "ix_trip_booking_reschedules_order_id");
    }
}
