using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(e => e.CategoryId).HasName("categories_pkey");

        builder.ToTable("categories");

        builder.Property(e => e.CategoryId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("category_id");
        builder.Property(e => e.CategoryType)
            .HasMaxLength(50)
            .HasColumnName("category_type");
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
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
    }
}
