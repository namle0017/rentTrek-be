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
        builder.ToTable("tour_provided_items", table =>
        {
            table.HasCheckConstraint("tour_provided_items_quantity_check", "(quantity > 0)");
        });
        builder.HasKey(p => p.Id).HasName("tour_provided_items_pkey");
        builder.Property(p => p.Id).HasColumnName("tour_provided_item_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.QuantityBasis).IsRequired().HasMaxLength(50);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.ItemName).HasDefaultValueSql("''::character varying");
        builder.Property(x => x.ProductVariantId).IsRequired();
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.ItemName).HasMaxLength(255);
        builder.HasIndex(x => x.ProductVariantId, "ix_tour_provided_items_product_variant_id");
        builder.HasIndex(x => x.TourId, "ix_tour_provided_items_tour_id");
        builder.HasOne(x => x.ProductVariant)
            .WithMany(x => x.TourProvidedItems)
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("tour_provided_items_product_variant_id_fkey");
        builder.HasOne(x => x.Tour)
            .WithMany(x => x.ProvidedItems)
            .HasForeignKey(x => x.TourId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("tour_provided_items_tour_id_fkey");
    }
}
