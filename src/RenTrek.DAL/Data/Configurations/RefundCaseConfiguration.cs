using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho hồ sơ hoàn tiền.
/// </summary>
public sealed class RefundCaseConfiguration : IEntityTypeConfiguration<RefundCase>
{
    public void Configure(EntityTypeBuilder<RefundCase> builder)
    {
        builder.ToTable("refund_cases");
        builder.HasKey(x => x.RefundCaseId);
        builder.Property(x => x.RefundCaseId).HasColumnName("refund_case_id");
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.RefundPaymentId).HasColumnName("refund_payment_id");
        builder.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        builder.Property(x => x.ApprovedByAdminUserId).HasColumnName("approved_by_admin_user_id");
        builder.Property(x => x.RefundPolicyRuleId).HasColumnName("refund_policy_rule_id");
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 0);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(40);
        builder.Property(x => x.BankCode).HasColumnName("bank_code").HasMaxLength(20);
        builder.Property(x => x.BankAccountLast4).HasColumnName("bank_account_last4").HasMaxLength(4).IsFixedLength();
        builder.Property(x => x.PayoutDetailsCiphertext).HasColumnName("payout_details_ciphertext");
        builder.Property(x => x.PayoutDetailsNonce).HasColumnName("payout_details_nonce");
        builder.Property(x => x.PayoutDetailsTag).HasColumnName("payout_details_tag");
        builder.Property(x => x.ConsentVersion).HasColumnName("consent_version").HasMaxLength(50);
        builder.Property(x => x.ConsentAcceptedAt).HasColumnName("consent_accepted_at");
        builder.Property(x => x.ProviderTransactionId).HasColumnName("provider_transaction_id").HasMaxLength(255);
        builder.Property(x => x.ApprovedAt).HasColumnName("approved_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.SensitiveDataPurgedAt).HasColumnName("sensitive_data_purged_at");
        builder.Property(x => x.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(1000);
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");

        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId);
        builder.HasOne(x => x.RefundPayment).WithMany().HasForeignKey(x => x.RefundPaymentId);
        builder.HasOne(x => x.CustomerUser).WithMany().HasForeignKey(x => x.CustomerUserId);
        builder.HasOne(x => x.RefundPolicyRule).WithMany().HasForeignKey(x => x.RefundPolicyRuleId);
    }
}
