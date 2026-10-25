using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");
        builder.HasKey(variant => variant.Id);
        builder.Property(variant => variant.Id).HasColumnName("product_variant_id");
        builder.Property(variant => variant.Sku).HasMaxLength(100).IsRequired();
        builder.Property(variant => variant.Name).HasColumnName("variant_name").HasMaxLength(255).IsRequired();
        builder.Property(variant => variant.Size).HasMaxLength(50);
        builder.Property(variant => variant.Color).HasMaxLength(50);
        builder.Property(variant => variant.Status).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(variant => variant.Sku).IsUnique().HasDatabaseName("UQ_ProductVariants_Sku");
        builder.HasOne(variant => variant.Product).WithMany(product => product.Variants)
            .HasForeignKey(variant => variant.ProductId).OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(variant => !variant.IsDeleted && !variant.Product.IsDeleted
            && !variant.Product.Category.IsDeleted);
    }
}
