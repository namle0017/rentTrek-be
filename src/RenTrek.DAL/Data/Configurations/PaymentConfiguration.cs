using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(e => e.PaymentId).HasName("payments_pkey");

        builder.ToTable("payments");

        builder.HasIndex(e => e.OrderId, "ix_payments_order_id");
        builder.HasIndex(e => e.OriginalPaymentId, "ix_payments_original_payment_id");
        builder.HasIndex(e => e.RefundPolicyRuleId, "ix_payments_refund_policy_rule_id");

        builder.Property(e => e.PaymentId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("payment_id");
        builder.Property(e => e.Amount)
            .HasPrecision(18)
            .HasColumnName("amount");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.OrderId).HasColumnName("order_id");
        builder.Property(e => e.OriginalPaymentId).HasColumnName("original_payment_id");
        builder.Property(e => e.PaymentMethod)
            .HasMaxLength(50)
            .HasColumnName("payment_method");
        builder.Property(e => e.PaymentType)
            .HasMaxLength(50)
            .HasColumnName("payment_type");
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");
        builder.Property(e => e.ProviderTransactionId)
            .HasMaxLength(255)
            .HasColumnName("provider_transaction_id");
        builder.Property(e => e.Purpose)
            .HasMaxLength(50)
            .HasColumnName("purpose");
        builder.Property(e => e.RefundPolicyRuleId).HasColumnName("refund_policy_rule_id");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");

        builder.HasOne(d => d.Order).WithMany(p => p.Payments)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("payments_order_id_fkey");

        builder.HasOne(d => d.OriginalPayment).WithMany(p => p.InverseOriginalPayment)
            .HasForeignKey(d => d.OriginalPaymentId)
            .HasConstraintName("payments_original_payment_id_fkey");

        builder.HasOne(d => d.RefundPolicyRule).WithMany(p => p.Payments)
            .HasForeignKey(d => d.RefundPolicyRuleId)
            .HasConstraintName("payments_refund_policy_rule_id_fkey");
    }
}
