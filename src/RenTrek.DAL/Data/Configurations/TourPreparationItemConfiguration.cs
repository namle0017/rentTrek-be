using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

// Tên cột snake_case do AppDbContext tự chuyển; chỉ khai báo chỗ khác quy ước.
public class TourPreparationItemConfiguration : IEntityTypeConfiguration<TourPreparationItem>
{
    public void Configure(EntityTypeBuilder<TourPreparationItem> builder)
    {
        builder.ToTable("tour_preparation_items");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("preparation_item_id");
        builder.Property(p => p.ItemName).IsRequired().HasMaxLength(255);
    }
}
