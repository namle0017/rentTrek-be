using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

// Tên cột snake_case do AppDbContext tự chuyển; chỉ khai báo chỗ khác quy ước.
/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể tour provided item.
/// </summary>
public class TourProvidedItemConfiguration : IEntityTypeConfiguration<TourProvidedItem>
{
    public void Configure(EntityTypeBuilder<TourProvidedItem> builder)
    {
        builder.ToTable("tour_provided_items");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("tour_provided_item_id");
        builder.Property(p => p.QuantityBasis).IsRequired().HasMaxLength(50);
    }
}
