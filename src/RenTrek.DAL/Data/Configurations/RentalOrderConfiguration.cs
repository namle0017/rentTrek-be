using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RentalOrderConfiguration : IEntityTypeConfiguration<RentalOrder>
{
    public void Configure(EntityTypeBuilder<RentalOrder> builder)
    {
        builder.HasKey(e => e.OrderId).HasName("rental_orders_pkey");

        builder.ToTable("rental_orders", table =>
        {
            table.HasCheckConstraint("rental_orders_check", "(rental_end_at > rental_start_at)");
        });

        builder.HasIndex(e => e.HandledByUserId, "ix_rental_orders_handled_by_user_id");

        builder.Property(e => e.OrderId)
            .ValueGeneratedNever()
            .HasColumnName("order_id");
        builder.Property(e => e.DepositAmount)
            .HasPrecision(18)
            .HasColumnName("deposit_amount");
        builder.Property(e => e.HandledByUserId).HasColumnName("handled_by_user_id");
        builder.Property(e => e.RentalEndAt).HasColumnName("rental_end_at");
        builder.Property(e => e.RentalStartAt).HasColumnName("rental_start_at");
        builder.Property(e => e.RentalPolicyId).HasColumnName("rental_policy_id");
        builder.Property(e => e.HandedOverAt).HasColumnName("handed_over_at");
        builder.Property(e => e.CustomerReceivedConfirmedAt).HasColumnName("customer_received_confirmed_at");
        builder.Property(e => e.ReceiptConfirmSource)
            .HasMaxLength(20)
            .HasColumnName("receipt_confirm_source");
        builder.Property(e => e.CancellationType)
            .HasMaxLength(30)
            .HasColumnName("cancellation_type");

        builder.HasIndex(e => e.RentalPolicyId, "ix_rental_orders_rental_policy_id");
        builder.HasIndex(e => e.CancellationType, "ix_rental_orders_cancellation_type")
            .HasFilter("(cancellation_type IS NOT NULL)");

        builder.HasOne(d => d.RentalPolicy).WithMany()
            .HasForeignKey(d => d.RentalPolicyId)
            .HasConstraintName("rental_orders_rental_policy_id_fkey");

        builder.HasOne(d => d.HandledByUser).WithMany(p => p.RentalOrders)
            .HasForeignKey(d => d.HandledByUserId)
            .HasConstraintName("rental_orders_handled_by_user_id_fkey");

        builder.HasOne(d => d.Order).WithOne(p => p.RentalOrder)
            .HasForeignKey<RentalOrder>(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_orders_order_id_fkey");
    }
}
