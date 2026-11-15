using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho bảng refund_proofs.
/// </summary>
public sealed class RefundProofConfiguration : IEntityTypeConfiguration<RefundProof>
{
    public void Configure(EntityTypeBuilder<RefundProof> builder)
    {
        builder.ToTable("refund_proofs", table =>
        {
            table.HasCheckConstraint("ck_refund_proofs_owner", "(((refund_case_id IS NOT NULL) AND (payment_id IS NULL)) OR ((refund_case_id IS NULL) AND (payment_id IS NOT NULL)))");
        });
        builder.HasKey(x => x.RefundProofId).HasName("refund_proofs_pkey");
        // Khóa do ứng dụng sinh để thêm qua navigation của hồ sơ vẫn được EF coi là bản ghi mới
        builder.Property(x => x.RefundProofId).HasColumnName("refund_proof_id").ValueGeneratedNever();
        builder.Property(x => x.RefundCaseId).HasColumnName("refund_case_id");
        builder.Property(x => x.PaymentId).HasColumnName("payment_id");
        builder.Property(x => x.StoragePath).HasColumnName("storage_path").HasMaxLength(500);
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100);
        builder.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255);
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        builder.Property(x => x.UploadedByUserId).HasColumnName("uploaded_by_user_id");
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(x => x.RefundCaseId, "ix_refund_proofs_refund_case").HasFilter("(NOT is_deleted)");
        builder.HasIndex(x => x.PaymentId, "ix_refund_proofs_payment").HasFilter("(NOT is_deleted)");

        builder.HasOne(x => x.RefundCase).WithMany(x => x.Proofs)
            .HasForeignKey(x => x.RefundCaseId)
            .HasConstraintName("refund_proofs_refund_case_id_fkey");
        builder.HasOne<Payment>().WithMany()
            .HasForeignKey(x => x.PaymentId)
            .HasConstraintName("refund_proofs_payment_id_fkey");
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(x => x.UploadedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("refund_proofs_uploaded_by_user_id_fkey");
    }
}
