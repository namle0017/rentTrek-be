using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể payment.
/// </summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(x => x.PaymentId).HasName("payments_pkey");
        builder.Property(x => x.PaymentId).HasColumnName("payment_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.OriginalPaymentId).HasColumnName("original_payment_id");
        builder.Property(x => x.PaymentType).HasColumnName("payment_type").HasMaxLength(50);
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 0);
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(50);
        builder.Property(x => x.ProviderTransactionId).HasColumnName("provider_transaction_id").HasMaxLength(255);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        builder.Property(x => x.RefundPolicyRuleId).HasColumnName("refund_policy_rule_id");
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(50);

        builder.Property(x => x.ProviderOrderId).HasColumnName("provider_order_id").HasMaxLength(100);
        builder.HasIndex(x => x.OrderId, "ix_payments_order_id");
        builder.HasIndex(x => x.OriginalPaymentId, "ix_payments_original_payment_id");
        builder.HasIndex(x => x.RefundPolicyRuleId, "ix_payments_refund_policy_rule_id");
        builder.HasIndex(x => new { x.OrderId, x.PaymentType, x.Status }, "ux_payments_one_pending_collection")
            .IsUnique()
            .HasFilter("(((payment_type)::text = 'Collection'::text) AND ((status)::text = 'Pending'::text))");
        builder.HasIndex(x => new { x.ProviderTransactionId, x.PaymentType }, "ux_payments_provider_txn")
            .IsUnique()
            .HasFilter("(provider_transaction_id IS NOT NULL)");
        builder.HasOne(x => x.Order)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("payments_order_id_fkey");
        builder.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(x => x.OriginalPaymentId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("payments_original_payment_id_fkey");
        builder.HasOne<RefundPolicyRule>()
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.RefundPolicyRuleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("payments_refund_policy_rule_id_fkey");
    }
}
