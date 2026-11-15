using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể product variant.
/// </summary>
public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");
        builder.HasKey(variant => variant.Id).HasName("product_variants_pkey");
        builder.Property(variant => variant.Id).HasColumnName("product_variant_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(variant => variant.Sku).HasMaxLength(100).IsRequired();
        builder.Property(variant => variant.Name).HasColumnName("variant_name").HasMaxLength(255).IsRequired();
        builder.Property(variant => variant.Size).HasMaxLength(50);
        builder.Property(variant => variant.Color).HasMaxLength(50);
        builder.Property(variant => variant.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(variant => variant.ReplacementValue).HasPrecision(18).HasColumnName("replacement_value");
        builder.HasQueryFilter(variant => !variant.IsDeleted && !variant.Product.IsDeleted
            && !variant.Product.Category.IsDeleted);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.ProductId, "ix_product_variants_product_id");
        builder.HasIndex(x => x.Sku, "product_variants_sku_key").IsUnique();
        builder.HasOne(x => x.Product)
            .WithMany(x => x.Variants)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("product_variants_product_id_fkey");
    }
}
