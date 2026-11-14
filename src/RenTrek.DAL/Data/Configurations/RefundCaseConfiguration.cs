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
        builder.ToTable("refund_cases", table =>
        {
            table.HasCheckConstraint("ck_refund_cases_payout_details", "(((payout_details_ciphertext IS NOT NULL) AND (payout_details_nonce IS NOT NULL) AND (payout_details_tag IS NOT NULL) AND (bank_code IS NOT NULL) AND (bank_account_last4 IS NOT NULL) AND (consent_version IS NOT NULL) AND (consent_accepted_at IS NOT NULL)) OR ((payout_details_ciphertext IS NULL) AND (payout_details_nonce IS NULL) AND (payout_details_tag IS NULL) AND ((sensitive_data_purged_at IS NOT NULL) OR ((status)::text = ANY ((ARRAY['AwaitingBankInfo'::character varying, 'Cancelled'::character varying])::text[])))))");
            table.HasCheckConstraint("refund_cases_amount_check", "(amount > (0)::numeric)");
            table.HasCheckConstraint("refund_cases_row_version_check", "(row_version >= 1)");
        });
        builder.HasKey(x => x.RefundCaseId).HasName("refund_cases_pkey");
        builder.Property(x => x.RefundCaseId).HasColumnName("refund_case_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.RefundPaymentId).HasColumnName("refund_payment_id");
        builder.Property(x => x.CustomerUserId).HasColumnName("customer_user_id");
        builder.Property(x => x.ApprovedByAdminUserId).HasColumnName("approved_by_admin_user_id");
        builder.Property(x => x.RefundPolicyRuleId).HasColumnName("refund_policy_rule_id");
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 0);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(40);
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(40).HasDefaultValue("TripCancellation");
        builder.Property(x => x.BankInfoRequestedAt).HasColumnName("bank_info_requested_at");
        builder.Property(x => x.BankInfoSubmittedAt).HasColumnName("bank_info_submitted_at");
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
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken().HasDefaultValue(1);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(x => x.ApprovedByAdminUserId, "IX_refund_cases_approved_by_admin_user_id");
        builder.HasIndex(x => x.CustomerUserId, "IX_refund_cases_customer_user_id");
        builder.HasIndex(x => x.RefundPolicyRuleId, "IX_refund_cases_refund_policy_rule_id");
        builder.HasIndex(x => x.OrderId, "ix_refund_cases_order");
        builder.HasIndex(x => new { x.Status, x.CreatedAt }, "ix_refund_cases_status_created")
            .HasFilter("(NOT is_deleted)").IsDescending(false, true);
        builder.HasIndex(x => x.RefundPaymentId, "refund_cases_refund_payment_id_key").IsUnique();
        builder.HasIndex(x => new { x.Source, x.Status }, "ix_refund_cases_source_status").HasFilter("(NOT is_deleted)");
        builder.HasIndex(x => x.OrderId, "ux_refund_cases_open_order")
            .IsUnique()
            .HasFilter("((NOT is_deleted) AND ((status)::text <> ALL ((ARRAY['Completed'::character varying, 'Rejected'::character varying, 'Cancelled'::character varying])::text[])))");
        builder.HasIndex(x => x.ProviderTransactionId, "ux_refund_cases_provider_transaction")
            .IsUnique()
            .HasFilter("(provider_transaction_id IS NOT NULL)");
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.ApprovedByAdminUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("refund_cases_approved_by_admin_user_id_fkey");
        builder.HasOne(x => x.CustomerUser)
            .WithMany()
            .HasForeignKey(x => x.CustomerUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("refund_cases_customer_user_id_fkey");
        // Index chỉ giới hạn hồ sơ đang mở; một đơn vẫn có nhiều hồ sơ lịch sử.
        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("refund_cases_order_id_fkey");
        builder.HasOne(x => x.RefundPayment)
            .WithOne()
            .HasForeignKey<RefundCase>(x => x.RefundPaymentId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("refund_cases_refund_payment_id_fkey");
        builder.HasOne(x => x.RefundPolicyRule)
            .WithMany()
            .HasForeignKey(x => x.RefundPolicyRuleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("refund_cases_refund_policy_rule_id_fkey");
    }
}
