using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TourImageConfiguration : IEntityTypeConfiguration<TourImage>
{
    public void Configure(EntityTypeBuilder<TourImage> builder)
    {
        builder.HasKey(e => e.TourImageId).HasName("tour_images_pkey");

        builder.ToTable("tour_images");

        builder.HasIndex(e => e.TourId, "ix_tour_images_tour_id");

        builder.Property(e => e.TourImageId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("tour_image_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.DisplayOrder)
            .HasDefaultValue(0)
            .HasColumnName("display_order");
        builder.Property(e => e.ImageUrl).HasColumnName("image_url");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.IsPrimary)
            .HasDefaultValue(false)
            .HasColumnName("is_primary");
        builder.Property(e => e.TourId).HasColumnName("tour_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Tour).WithMany(p => p.TourImages)
            .HasForeignKey(d => d.TourId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tour_images_tour_id_fkey");
    }
}
