using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class ProductVariantImageConfiguration : IEntityTypeConfiguration<ProductVariantImage>
{
    public void Configure(EntityTypeBuilder<ProductVariantImage> builder)
    {
        builder.HasKey(e => e.ImageId).HasName("product_variant_images_pkey");

        builder.ToTable("product_variant_images");

        builder.HasIndex(e => e.ProductVariantId, "ix_product_variant_images_product_variant_id");

        builder.Property(e => e.ImageId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("image_id");
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
        builder.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.ProductVariant).WithMany(p => p.ProductVariantImages)
            .HasForeignKey(d => d.ProductVariantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("product_variant_images_product_variant_id_fkey");
    }
}
