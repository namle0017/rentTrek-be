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
        builder.HasKey(x => x.PaymentId);
        builder.Property(x => x.PaymentId).HasColumnName("payment_id");
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.OriginalPaymentId).HasColumnName("original_payment_id");
        builder.Property(x => x.PaymentType).HasColumnName("payment_type").HasMaxLength(50);
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 0);
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(50);
        builder.Property(x => x.ProviderTransactionId).HasColumnName("provider_transaction_id").HasMaxLength(255);
        builder.Property(x => x.TransferCode).HasMaxLength(40);
        builder.HasIndex(x => x.TransferCode).IsUnique();
        builder.Property(x => x.ReceivedAmount).HasPrecision(18, 0);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        builder.Property(x => x.RefundPolicyRuleId).HasColumnName("refund_policy_rule_id");
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(50);
        builder.HasOne(x => x.Order).WithMany(x => x.Payments).HasForeignKey(x => x.OrderId);
    }
}
