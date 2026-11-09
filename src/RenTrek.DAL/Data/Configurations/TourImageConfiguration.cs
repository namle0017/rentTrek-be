using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể tour image.
/// </summary>
public class TourImageConfiguration : IEntityTypeConfiguration<TourImage>
{
    public void Configure(EntityTypeBuilder<TourImage> builder)
    {
        builder.ToTable("tour_images");

        builder.HasKey(i => i.Id).HasName("tour_images_pkey");
        builder.Property(i => i.Id).HasColumnName("tour_image_id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.ImageUrl)
            .IsRequired().HasColumnType("text");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.IsPrimary).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.TourId, "ix_tour_images_tour_id");
        builder.HasOne(x => x.Tour)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.TourId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("tour_images_tour_id_fkey");
    }
}
