using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể order.
/// </summary>
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(x => x.OrderId);
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        builder.Property(x => x.OrderType).HasColumnName("order_type").HasMaxLength(50);
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 0);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.OrderCode).HasColumnName("order_code").HasMaxLength(50);
        builder.Property(x => x.Note).HasColumnName("note");
        builder.Property(x => x.SubtotalAmount).HasColumnName("subtotal_amount").HasPrecision(18, 0);
        builder.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasPrecision(18, 0);
        builder.Property(x => x.PaymentStatus).HasColumnName("payment_status").HasMaxLength(50);
        builder.Property(x => x.HoldExpiresAt).HasColumnName("hold_expires_at");
        builder.Property(x => x.ReconciliationDeadlineAt).HasColumnName("reconciliation_deadline_at");
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.CancelReason).HasColumnName("cancel_reason").HasMaxLength(500);
        builder.Property(x => x.RefundPolicyId).HasColumnName("refund_policy_id");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100);
        builder.HasIndex(x => new { x.CustomerUserId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL");
        builder.HasOne(x => x.CustomerUser).WithMany(x => x.Orders).HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
