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
        builder.ToTable("trip_booking_orders");
        builder.HasKey(x => x.OrderId);
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.TripId).HasColumnName("trip_id");
        builder.Property(x => x.ParticipantCount).HasColumnName("participant_count");
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 0);
        builder.Property(x => x.GroupingPreference).HasColumnName("grouping_preference").HasMaxLength(50);
        builder.Property(x => x.IsSplit).HasColumnName("is_split");
        builder.Property(x => x.SplitReason).HasColumnName("split_reason");
        builder.Property(x => x.SplitApprovedByUserId).HasColumnName("split_approved_by_user_id");
        builder.Property(x => x.SplitAt).HasColumnName("split_at");
        builder.HasOne(x => x.Order).WithOne(x => x.TripBookingOrder).HasForeignKey<TripBookingOrder>(x => x.OrderId);
        builder.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
    }
}
