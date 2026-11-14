using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class ProductBundleConfiguration : IEntityTypeConfiguration<ProductBundle>
{
    public void Configure(EntityTypeBuilder<ProductBundle> builder)
    {
        builder.HasKey(e => e.BundleId).HasName("product_bundles_pkey");

        builder.ToTable("product_bundles");

        builder.HasIndex(e => e.CategoryId, "ix_product_bundles_category_id");

        builder.Property(e => e.BundleId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("bundle_id");
        builder.Property(e => e.CategoryId).HasColumnName("category_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.Name)
            .HasMaxLength(255)
            .HasColumnName("name");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.ReplacementValue)
            .HasPrecision(18)
            .HasColumnName("replacement_value");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Category).WithMany(p => p.ProductBundles)
            .HasForeignKey(d => d.CategoryId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("product_bundles_category_id_fkey");
    }
}
