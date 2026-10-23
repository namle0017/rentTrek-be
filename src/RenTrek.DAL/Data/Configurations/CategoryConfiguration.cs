using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).HasMaxLength(150).IsRequired();
        builder.Property(category => category.Status).HasConversion<string>().HasMaxLength(30);
        builder.HasQueryFilter(category => !category.IsDeleted);
    }
}
