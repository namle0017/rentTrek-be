using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho bảng lịch sử điều chỉnh đơn thuê rental_order_adjustments.
/// </summary>
public class RentalOrderAdjustmentConfiguration : IEntityTypeConfiguration<RentalOrderAdjustment>
{
    public void Configure(EntityTypeBuilder<RentalOrderAdjustment> builder)
    {
        builder.ToTable("rental_order_adjustments", table =>
        {
            table.HasCheckConstraint("rental_order_adjustments_extra_amount_check", "(extra_amount >= (0)::numeric)");
        });
        builder.HasKey(e => e.RentalOrderAdjustmentId).HasName("rental_order_adjustments_pkey");

        builder.HasIndex(e => e.RentalOrderId, "ix_rental_order_adjustments_rental_order_id");
        builder.HasIndex(e => e.RequestedByUserId, "ix_rental_order_adjustments_requested_by_user_id");

        builder.Property(e => e.RentalOrderAdjustmentId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("rental_order_adjustment_id");
        builder.Property(e => e.RentalOrderId).HasColumnName("rental_order_id");
        builder.Property(e => e.AdjustmentType)
            .HasMaxLength(30)
            .HasColumnName("adjustment_type");
        builder.Property(e => e.OldEndAt).HasColumnName("old_end_at");
        builder.Property(e => e.NewEndAt).HasColumnName("new_end_at");
        builder.Property(e => e.ExtraAmount)
            .HasPrecision(12, 2)
            .HasColumnName("extra_amount");
        builder.Property(e => e.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");

        builder.HasOne(d => d.RentalOrder).WithMany()
            .HasForeignKey(d => d.RentalOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_order_adjustments_rental_order_id_fkey");

        builder.HasOne(d => d.RequestedByUser).WithMany()
            .HasForeignKey(d => d.RequestedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_order_adjustments_requested_by_user_id_fkey");
    }
}
