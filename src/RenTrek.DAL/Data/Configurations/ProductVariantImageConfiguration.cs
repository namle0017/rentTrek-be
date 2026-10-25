using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class ProductVariantImageConfiguration : IEntityTypeConfiguration<ProductVariantImage>
{
    public void Configure(EntityTypeBuilder<ProductVariantImage> builder)
    {
        builder.ToTable("product_variant_images");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).HasColumnName("image_id");
        builder.Property(image => image.ImageUrl).HasMaxLength(2048).IsRequired();
        builder.HasOne(image => image.ProductVariant).WithMany(variant => variant.Images)
            .HasForeignKey(image => image.ProductVariantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(image => !image.IsDeleted && !image.ProductVariant.IsDeleted
            && !image.ProductVariant.Product.IsDeleted && !image.ProductVariant.Product.Category.IsDeleted);
    }
}
