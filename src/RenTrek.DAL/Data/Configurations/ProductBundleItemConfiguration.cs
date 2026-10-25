using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class ProductBundleItemConfiguration : IEntityTypeConfiguration<ProductBundleItem>
{
    public void Configure(EntityTypeBuilder<ProductBundleItem> builder)
    {
        builder.HasKey(e => e.BundleItemId).HasName("product_bundle_items_pkey");

        builder.ToTable("product_bundle_items");

        builder.HasIndex(e => e.BundleId, "ix_product_bundle_items_bundle_id");
        builder.HasIndex(e => e.ProductVariantId, "ix_product_bundle_items_product_variant_id");

        builder.Property(e => e.BundleItemId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("bundle_item_id");
        builder.Property(e => e.BundleId).HasColumnName("bundle_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(e => e.Quantity).HasColumnName("quantity");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Bundle).WithMany(p => p.ProductBundleItems)
            .HasForeignKey(d => d.BundleId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("product_bundle_items_bundle_id_fkey");

        builder.HasOne(d => d.ProductVariant).WithMany(p => p.ProductBundleItems)
            .HasForeignKey(d => d.ProductVariantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("product_bundle_items_product_variant_id_fkey");
    }
}
