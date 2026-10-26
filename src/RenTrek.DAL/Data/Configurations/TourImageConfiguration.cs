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

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("tour_image_id");

        builder.Property(i => i.ImageUrl)
            .IsRequired()
            .HasMaxLength(2000);
    }
}
