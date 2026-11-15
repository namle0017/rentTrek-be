using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho bảng rental_reports.
/// </summary>
public class RentalReportConfiguration : IEntityTypeConfiguration<RentalReport>
{
    public void Configure(EntityTypeBuilder<RentalReport> builder)
    {
        builder.HasKey(e => e.RentalReportId).HasName("rental_reports_pkey");
        builder.ToTable("rental_reports", table =>
        {
            table.HasCheckConstraint("rental_reports_report_type_check", "((report_type)::text = ANY ((ARRAY['NoShow'::character varying, 'LateCancellation'::character varying, 'ReturnInspection'::character varying, 'SuspectedLoss'::character varying, 'ExtraPayment'::character varying])::text[]))");
            table.HasCheckConstraint("rental_reports_status_check", "((status)::text = ANY ((ARRAY['Submitted'::character varying, 'Approved'::character varying, 'Rejected'::character varying, 'Resolved'::character varying])::text[]))");
            table.HasCheckConstraint("rental_reports_amount_check", "(amount >= (0)::numeric)");
        });

        builder.HasIndex(e => new { e.Status, e.CreatedAt }, "ix_rental_reports_status_created").IsDescending(false, true);
        builder.HasIndex(e => e.RentalOrderId, "ix_rental_reports_order");
        builder.HasIndex(e => new { e.RentalOrderId, e.ReportType }, "ux_rental_reports_open_type")
            .IsUnique()
            .HasFilter("((status)::text = 'Submitted'::text)");

        builder.Property(e => e.RentalReportId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("rental_report_id");
        builder.Property(e => e.RentalOrderId).HasColumnName("rental_order_id");
        builder.Property(e => e.ReportType).HasMaxLength(30).HasColumnName("report_type");
        builder.Property(e => e.Status).HasMaxLength(30).HasColumnName("status");
        builder.Property(e => e.Amount).HasPrecision(18).HasDefaultValue(0m).HasColumnName("amount");
        builder.Property(e => e.Note).HasMaxLength(2000).HasColumnName("note");
        builder.Property(e => e.ReviewNote).HasMaxLength(1000).HasColumnName("review_note");
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(e => e.RowVersion).IsConcurrencyToken().HasDefaultValue(1).HasColumnName("row_version");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");

        builder.HasOne(d => d.RentalOrder).WithMany()
            .HasForeignKey(d => d.RentalOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_reports_rental_order_id_fkey");
        builder.HasOne(d => d.CreatedByUser).WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .HasConstraintName("rental_reports_created_by_user_id_fkey");
        builder.HasOne(d => d.ReviewedByUser).WithMany()
            .HasForeignKey(d => d.ReviewedByUserId)
            .HasConstraintName("rental_reports_reviewed_by_user_id_fkey");
    }
}
