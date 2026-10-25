using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TourProvidedItemConfiguration : IEntityTypeConfiguration<TourProvidedItem>
{
    public void Configure(EntityTypeBuilder<TourProvidedItem> builder)
    {
        builder.HasKey(e => e.TourProvidedItemId).HasName("tour_provided_items_pkey");

        builder.ToTable("tour_provided_items");

        builder.HasIndex(e => e.ProductVariantId, "ix_tour_provided_items_product_variant_id");
        builder.HasIndex(e => e.TourId, "ix_tour_provided_items_tour_id");

        builder.Property(e => e.TourProvidedItemId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("tour_provided_item_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(e => e.Quantity).HasColumnName("quantity");
        builder.Property(e => e.QuantityBasis)
            .HasMaxLength(50)
            .HasColumnName("quantity_basis");
        builder.Property(e => e.TourId).HasColumnName("tour_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.ProductVariant).WithMany(p => p.TourProvidedItems)
            .HasForeignKey(d => d.ProductVariantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tour_provided_items_product_variant_id_fkey");

        builder.HasOne(d => d.Tour).WithMany(p => p.TourProvidedItems)
            .HasForeignKey(d => d.TourId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tour_provided_items_tour_id_fkey");
    }
}
