using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TripBookingOrderConfiguration : IEntityTypeConfiguration<TripBookingOrder>
{
    public void Configure(EntityTypeBuilder<TripBookingOrder> builder)
    {
        builder.HasKey(e => e.OrderId).HasName("trip_booking_orders_pkey");

        builder.ToTable("trip_booking_orders");

        builder.HasIndex(e => e.SplitApprovedByUserId, "ix_trip_booking_orders_split_approved_by_user_id");
        builder.HasIndex(e => e.TripId, "ix_trip_booking_orders_trip_id");

        builder.Property(e => e.OrderId)
            .ValueGeneratedNever()
            .HasColumnName("order_id");
        builder.Property(e => e.GroupingPreference)
            .HasMaxLength(50)
            .HasColumnName("grouping_preference");
        builder.Property(e => e.IsSplit)
            .HasDefaultValue(false)
            .HasColumnName("is_split");
        builder.Property(e => e.ParticipantCount).HasColumnName("participant_count");
        builder.Property(e => e.SplitApprovedByUserId).HasColumnName("split_approved_by_user_id");
        builder.Property(e => e.SplitAt).HasColumnName("split_at");
        builder.Property(e => e.SplitReason).HasColumnName("split_reason");
        builder.Property(e => e.TripId).HasColumnName("trip_id");
        builder.Property(e => e.UnitPrice)
            .HasPrecision(18)
            .HasColumnName("unit_price");

        builder.HasOne(d => d.Order).WithOne(p => p.TripBookingOrder)
            .HasForeignKey<TripBookingOrder>(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("trip_booking_orders_order_id_fkey");

        builder.HasOne(d => d.SplitApprovedByUser).WithMany(p => p.TripBookingOrders)
            .HasForeignKey(d => d.SplitApprovedByUserId)
            .HasConstraintName("trip_booking_orders_split_approved_by_user_id_fkey");

        builder.HasOne(d => d.Trip).WithMany(p => p.TripBookingOrders)
            .HasForeignKey(d => d.TripId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("trip_booking_orders_trip_id_fkey");
    }
}
