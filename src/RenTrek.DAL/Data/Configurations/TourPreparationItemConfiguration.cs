using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

// Tên cột snake_case do AppDbContext tự chuyển; chỉ khai báo chỗ khác quy ước.
/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể tour preparation item.
/// </summary>
public class TourPreparationItemConfiguration : IEntityTypeConfiguration<TourPreparationItem>
{
    public void Configure(EntityTypeBuilder<TourPreparationItem> builder)
    {
        builder.ToTable("tour_preparation_items");
        builder.HasKey(p => p.Id).HasName("tour_preparation_items_pkey");
        builder.Property(p => p.Id).HasColumnName("preparation_item_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.ItemName).IsRequired().HasMaxLength(255);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.TourId, "ix_tour_preparation_items_tour_id");
        builder.HasOne(x => x.Tour)
            .WithMany(x => x.PreparationItems)
            .HasForeignKey(x => x.TourId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("tour_preparation_items_tour_id_fkey");
    }
}
