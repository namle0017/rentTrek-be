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
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).HasColumnName("product_id");
        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
        builder.Property(product => product.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(product => product.CreatedAt).HasConversion(
            value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        builder.HasOne(product => product.Category).WithMany(category => category.Products)
            .HasForeignKey(product => product.CategoryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(product => new { product.Status, product.CreatedAt, product.Id });
        builder.HasQueryFilter(product => !product.IsDeleted && !product.Category.IsDeleted);
    }
}
