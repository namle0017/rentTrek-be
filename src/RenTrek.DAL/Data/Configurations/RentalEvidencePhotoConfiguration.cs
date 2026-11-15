using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho bảng rental_evidence_photos.
/// </summary>
public class RentalEvidencePhotoConfiguration : IEntityTypeConfiguration<RentalEvidencePhoto>
{
    public void Configure(EntityTypeBuilder<RentalEvidencePhoto> builder)
    {
        builder.HasKey(e => e.RentalEvidencePhotoId).HasName("rental_evidence_photos_pkey");
        builder.ToTable("rental_evidence_photos", table =>
        {
            table.HasCheckConstraint("rental_evidence_photos_stage_check", "((stage)::text = ANY ((ARRAY['Handover'::character varying, 'Return'::character varying, 'Damage'::character varying, 'CustomerIssue'::character varying])::text[]))");
        });

        builder.HasIndex(e => new { e.RentalOrderId, e.Stage }, "ix_rental_evidence_photos_order_stage").HasFilter("(NOT is_deleted)");
        builder.HasIndex(e => e.AssetAssignmentId, "ix_rental_evidence_photos_assignment");

        builder.Property(e => e.RentalEvidencePhotoId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("rental_evidence_photo_id");
        builder.Property(e => e.RentalOrderId).HasColumnName("rental_order_id");
        builder.Property(e => e.AssetAssignmentId).HasColumnName("asset_assignment_id");
        builder.Property(e => e.Stage).HasMaxLength(30).HasColumnName("stage");
        builder.Property(e => e.ImageUrl).HasColumnName("image_url");
        builder.Property(e => e.Note).HasMaxLength(500).HasColumnName("note");
        builder.Property(e => e.UploadedByUserId).HasColumnName("uploaded_by_user_id");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        builder.Property(e => e.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");

        builder.HasOne(d => d.RentalOrder).WithMany()
            .HasForeignKey(d => d.RentalOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_evidence_photos_rental_order_id_fkey");
        builder.HasOne(d => d.AssetAssignment).WithMany()
            .HasForeignKey(d => d.AssetAssignmentId)
            .HasConstraintName("rental_evidence_photos_asset_assignment_id_fkey");
        builder.HasOne(d => d.UploadedByUser).WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_evidence_photos_uploaded_by_user_id_fkey");
    }
}
