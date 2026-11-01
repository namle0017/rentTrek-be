using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public sealed class PaymentLedgerConfiguration :
    IEntityTypeConfiguration<PaymentWebhookReceipt>, IEntityTypeConfiguration<BankTransaction>,
    IEntityTypeConfiguration<RefundRequest>, IEntityTypeConfiguration<RefundAllocation>,
    IEntityTypeConfiguration<PaymentIdempotencyKey>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookReceipt> b)
    {
        b.ToTable("payment_webhook_receipts"); b.HasKey(x => x.PaymentWebhookReceiptId);
        b.HasIndex(x => new { x.Provider, x.ProviderEventId }).IsUnique();
        b.Property(x => x.Provider).HasMaxLength(30); b.Property(x => x.ProviderEventId).HasMaxLength(100);
        b.Property(x => x.PayloadHash).HasMaxLength(64); b.Property(x => x.Status).HasMaxLength(30);
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
    public void Configure(EntityTypeBuilder<BankTransaction> b)
    {
        b.ToTable("bank_transactions", t => t.HasCheckConstraint("ck_bank_transaction_amount", "amount > 0 AND allocated_amount >= 0 AND allocated_amount <= amount"));
        b.HasKey(x => x.BankTransactionId);
        b.HasIndex(x => new { x.Provider, x.ProviderTransactionId }).IsUnique();
        b.Property(x => x.Provider).HasMaxLength(30); b.Property(x => x.ProviderTransactionId).HasMaxLength(100);
        b.Property(x => x.Amount).HasPrecision(18, 0); b.Property(x => x.AllocatedAmount).HasPrecision(18, 0);
        b.HasOne<PaymentWebhookReceipt>().WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.OrderId); b.HasIndex(x => x.Status);
    }
    public void Configure(EntityTypeBuilder<RefundRequest> b)
    {
        b.ToTable("refund_requests", t => {
            t.HasCheckConstraint("ck_refund_request_amount", "amount > 0");
            t.HasCheckConstraint("ck_refund_request_status", "status IN ('PendingReview','Approved','Rejected','Done')");
            t.HasCheckConstraint("ck_refund_done_evidence", "status <> 'Done' OR (completed_at IS NOT NULL AND completed_by IS NOT NULL AND transfer_reference IS NOT NULL AND transferred_at IS NOT NULL AND refund_payment_id IS NOT NULL)");
        });
        b.HasKey(x => x.RefundRequestId); b.HasIndex(x => new { x.CustomerUserId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.Status, x.SubmittedAt });
        b.HasIndex(x => x.RefundPaymentId).IsUnique();
        b.HasIndex(x => x.TransferReference).IsUnique().HasFilter("status = 'Done'")
            .HasDatabaseName("ux_refund_requests_transfer_reference");
        b.Property(x => x.IdempotencyKey).HasMaxLength(100); b.Property(x => x.RequestHash).HasMaxLength(64);
        b.Property(x => x.Amount).HasPrecision(18, 0); b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.Note).HasMaxLength(2000); b.Property(x => x.RejectReason).HasMaxLength(2000);
        b.Property(x => x.TransferReference).HasMaxLength(200); b.Property(x => x.EvidenceReference).HasMaxLength(1000);
        b.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.AssignedStaffId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RefundPolicy>().WithMany().HasForeignKey(x => x.RefundPolicyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RefundPolicyRule>().WithMany().HasForeignKey(x => x.RefundPolicyRuleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.RefundPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<RefundAllocation> b)
    {
        b.ToTable("refund_allocations", t => t.HasCheckConstraint("ck_refund_allocation_amount", "amount > 0"));
        b.HasKey(x => x.RefundAllocationId); b.Property(x => x.Amount).HasPrecision(18, 0);
        b.HasIndex(x => new { x.RefundRequestId, x.BankTransactionId }).IsUnique();
        b.HasOne<RefundRequest>().WithMany().HasForeignKey(x => x.RefundRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<BankTransaction>().WithMany().HasForeignKey(x => x.BankTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<PaymentIdempotencyKey> b)
    {
        b.ToTable("payment_idempotency_keys"); b.HasKey(x => x.PaymentIdempotencyKeyId);
        b.HasIndex(x => new { x.CustomerUserId, x.Key }).IsUnique(); b.Property(x => x.Key).HasMaxLength(100);
        b.Property(x => x.RequestHash).HasMaxLength(64);
        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}
