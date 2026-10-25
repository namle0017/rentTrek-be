using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(e => e.OrderId).HasName("orders_pkey");

        builder.ToTable("orders");

        builder.HasIndex(e => e.CustomerUserId, "ix_orders_customer_user_id");
        builder.HasIndex(e => e.RefundPolicyId, "ix_orders_refund_policy_id");
        builder.HasIndex(e => e.OrderCode, "orders_order_code_key").IsUnique();

        builder.Property(e => e.OrderId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("order_id");
        builder.Property(e => e.CancelReason)
            .HasMaxLength(500)
            .HasColumnName("cancel_reason");
        builder.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.CustomerUserId).HasColumnName("customer_user_id");
        builder.Property(e => e.DiscountAmount)
            .HasPrecision(18)
            .HasColumnName("discount_amount");
        builder.Property(e => e.HoldExpiresAt).HasColumnName("hold_expires_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.OrderCode)
            .HasMaxLength(50)
            .HasColumnName("order_code");
        builder.Property(e => e.OrderType)
            .HasMaxLength(50)
            .HasColumnName("order_type");
        builder.Property(e => e.PaymentStatus)
            .HasMaxLength(50)
            .HasColumnName("payment_status");
        builder.Property(e => e.RefundPolicyId).HasColumnName("refund_policy_id");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.SubtotalAmount)
            .HasPrecision(18)
            .HasColumnName("subtotal_amount");
        builder.Property(e => e.TotalAmount)
            .HasPrecision(18)
            .HasColumnName("total_amount");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.CustomerUser).WithMany(p => p.Orders)
            .HasForeignKey(d => d.CustomerUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("orders_customer_user_id_fkey");

        builder.HasOne(d => d.RefundPolicy).WithMany(p => p.Orders)
            .HasForeignKey(d => d.RefundPolicyId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("orders_refund_policy_id_fkey");
    }
}
