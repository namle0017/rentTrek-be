using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TourSuggestedBundleConfiguration : IEntityTypeConfiguration<TourSuggestedBundle>
{
    public void Configure(EntityTypeBuilder<TourSuggestedBundle> builder)
    {
        builder.ToTable("tour_suggested_bundles");
        builder.HasKey(e => e.TourSuggestedBundleId).HasName("tour_suggested_bundles_pkey");
        builder.HasIndex(e => new { e.TourId, e.BundleId }, "tour_suggested_bundles_tour_id_bundle_id_key").IsUnique();

        builder.Property(e => e.TourSuggestedBundleId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("tour_suggested_bundle_id");
        builder.Property(e => e.TourId).HasColumnName("tour_id");
        builder.Property(e => e.BundleId).HasColumnName("bundle_id");
        builder.Property(e => e.SortOrder).HasColumnName("sort_order");
        builder.Property(e => e.Note).HasMaxLength(500).HasColumnName("note");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

        // Cascade: bỏ khỏi Tour.SuggestedBundles là xoá dòng liên kết.
        builder.HasOne(e => e.Tour).WithMany(t => t.SuggestedBundles)
            .HasForeignKey(e => e.TourId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("tour_suggested_bundles_tour_id_fkey");
        builder.HasOne(e => e.Bundle).WithMany()
            .HasForeignKey(e => e.BundleId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tour_suggested_bundles_bundle_id_fkey");
    }
}
