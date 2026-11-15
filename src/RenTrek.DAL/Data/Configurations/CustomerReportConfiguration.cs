using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho bảng customer_reports (database/migrations/20261121_customer_reports.sql).
/// </summary>
public sealed class CustomerReportConfiguration : IEntityTypeConfiguration<CustomerReport>
{
    public void Configure(EntityTypeBuilder<CustomerReport> builder)
    {
        builder.HasKey(e => e.CustomerReportId).HasName("customer_reports_pkey");
        builder.ToTable("customer_reports", table =>
        {
            table.HasCheckConstraint("customer_reports_source_type_check", "source_type IN ('Rental', 'Trip')");
            table.HasCheckConstraint("customer_reports_status_check", "status IN ('Open', 'InProgress', 'Resolved', 'Rejected')");
            table.HasCheckConstraint("customer_reports_photo_count_check", "cardinality(photo_urls) <= 5");
        });

        builder.HasIndex(e => new { e.Status, e.CreatedAt }, "ix_customer_reports_status_created").IsDescending(false, true);
        builder.HasIndex(e => e.OrderId, "ix_customer_reports_order");
        builder.HasIndex(e => new { e.ReporterUserId, e.CreatedAt }, "ix_customer_reports_reporter").IsDescending(false, true);

        builder.Property(e => e.CustomerReportId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("customer_report_id");
        builder.Property(e => e.ReporterUserId).HasColumnName("reporter_user_id");
        builder.Property(e => e.OrderId).HasColumnName("order_id");
        builder.Property(e => e.SourceType).HasMaxLength(20).HasColumnName("source_type");
        builder.Property(e => e.Category).HasMaxLength(40).HasColumnName("category");
        builder.Property(e => e.SubjectLabel).HasMaxLength(255).HasColumnName("subject_label");
        builder.Property(e => e.Description).HasMaxLength(2000).HasColumnName("description");
        builder.Property(e => e.PreferredAction).HasMaxLength(40).HasColumnName("preferred_action");
        builder.Property(e => e.ContactPhone).HasMaxLength(20).HasColumnName("contact_phone");
        builder.Property(e => e.PhotoUrls).HasColumnType("text[]").HasColumnName("photo_urls");
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Open").HasColumnName("status");
        builder.Property(e => e.AdminNote).HasMaxLength(1000).HasColumnName("admin_note");
        builder.Property(e => e.HandledByUserId).HasColumnName("handled_by_user_id");
        builder.Property(e => e.HandledAt).HasColumnName("handled_at");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");

        builder.HasOne(d => d.ReporterUser).WithMany()
            .HasForeignKey(d => d.ReporterUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("customer_reports_reporter_user_id_fkey");
        builder.HasOne(d => d.Order).WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("customer_reports_order_id_fkey");
        builder.HasOne(d => d.HandledByUser).WithMany()
            .HasForeignKey(d => d.HandledByUserId)
            .HasConstraintName("customer_reports_handled_by_user_id_fkey");
    }
}
