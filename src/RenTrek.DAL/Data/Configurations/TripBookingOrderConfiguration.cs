using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể trip booking order.
/// </summary>
public sealed class TripBookingOrderConfiguration : IEntityTypeConfiguration<TripBookingOrder>
{
    public void Configure(EntityTypeBuilder<TripBookingOrder> builder)
    {
        builder.ToTable("trip_booking_orders", table =>
        {
            table.HasCheckConstraint("trip_booking_orders_participant_count_check", "(participant_count > 0)");
        });
        builder.HasKey(x => x.OrderId).HasName("trip_booking_orders_pkey");
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.TripId).HasColumnName("trip_id");
        builder.Property(x => x.ParticipantCount).HasColumnName("participant_count");
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 0);
        builder.Property(x => x.GroupingPreference).HasColumnName("grouping_preference").HasMaxLength(50);
        builder.Property(x => x.IsSplit).HasColumnName("is_split").HasDefaultValue(false);
        builder.Property(x => x.SplitReason).HasColumnName("split_reason");
        builder.Property(x => x.SplitApprovedByUserId).HasColumnName("split_approved_by_user_id");
        builder.Property(x => x.SplitAt).HasColumnName("split_at");
        builder.Property(x => x.InviteVersion).HasColumnName("invite_version").HasDefaultValue(0);

        builder.HasIndex(x => x.SplitApprovedByUserId, "ix_trip_booking_orders_split_approved_by_user_id");
        builder.HasIndex(x => x.TripId, "ix_trip_booking_orders_trip_id");
        builder.HasOne(x => x.Order)
            .WithOne(x => x.TripBookingOrder)
            .HasForeignKey<TripBookingOrder>(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("trip_booking_orders_order_id_fkey");
        builder.HasOne<Account>()
            .WithMany(x => x.TripBookingOrders)
            .HasForeignKey(x => x.SplitApprovedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("trip_booking_orders_split_approved_by_user_id_fkey");
        builder.HasOne(x => x.Trip)
            .WithMany(x => x.TripBookingOrders)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("trip_booking_orders_trip_id_fkey");
    }
}
