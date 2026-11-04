using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể product variant image.
/// </summary>
public class ProductVariantImageConfiguration : IEntityTypeConfiguration<ProductVariantImage>
{
    public void Configure(EntityTypeBuilder<ProductVariantImage> builder)
    {
        builder.ToTable("product_variant_images");
        builder.HasKey(image => image.Id).HasName("product_variant_images_pkey");
        builder.Property(image => image.Id).HasColumnName("image_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(image => image.ImageUrl).IsRequired().HasColumnType("text");
        builder.HasQueryFilter(image => !image.IsDeleted && !image.ProductVariant.IsDeleted
            && !image.ProductVariant.Product.IsDeleted && !image.ProductVariant.Product.Category.IsDeleted);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.IsPrimary).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.ProductVariantId, "ix_product_variant_images_product_variant_id");
        builder.HasOne(x => x.ProductVariant)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("product_variant_images_product_variant_id_fkey");
    }
}
