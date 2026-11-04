using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể product.
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(product => product.Id).HasName("products_pkey");
        builder.Property(product => product.Id).HasColumnName("product_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(product => product.Name).IsRequired().HasMaxLength(255);
        builder.Property(product => product.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(product => product.CreatedAt).HasConversion(
            value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc)).HasDefaultValueSql("now()");
        builder.HasQueryFilter(product => !product.IsDeleted && !product.Category.IsDeleted);

        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.CategoryId, "ix_products_category_id");
        builder.HasIndex(x => x.CreatedByUserId, "ix_products_created_by_user_id");
        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("products_category_id_fkey");
        builder.HasOne<Account>()
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("products_created_by_user_id_fkey");
    }
}
