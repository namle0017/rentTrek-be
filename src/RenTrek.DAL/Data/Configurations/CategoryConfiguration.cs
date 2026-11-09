using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể category.
/// </summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id).HasName("categories_pkey");
        builder.Property(c => c.Id).HasColumnName("category_id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.Name)
            .IsRequired().HasMaxLength(255);

        builder.Property(c => c.Description).HasColumnType("text");

        builder.Property(c => c.CategoryType)
            .IsRequired().HasMaxLength(50);

        builder.Property(c => c.Status)
            .IsRequired().HasMaxLength(50);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
    }
}
