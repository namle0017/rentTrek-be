using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.HasKey(e => e.ProductVariantId).HasName("product_variants_pkey");

        builder.ToTable("product_variants");

        builder.HasIndex(e => e.ProductId, "ix_product_variants_product_id");
        builder.HasIndex(e => e.Sku, "product_variants_sku_key").IsUnique();

        builder.Property(e => e.ProductVariantId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("product_variant_id");
        builder.Property(e => e.Color)
            .HasMaxLength(50)
            .HasColumnName("color");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.ProductId).HasColumnName("product_id");
        builder.Property(e => e.Size)
            .HasMaxLength(50)
            .HasColumnName("size");
        builder.Property(e => e.Sku)
            .HasMaxLength(100)
            .HasColumnName("sku");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
        builder.Property(e => e.VariantName)
            .HasMaxLength(255)
            .HasColumnName("variant_name");

        builder.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("product_variants_product_id_fkey");
    }
}
