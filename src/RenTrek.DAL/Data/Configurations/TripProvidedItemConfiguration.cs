using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TripProvidedItemConfiguration : IEntityTypeConfiguration<TripProvidedItem>
{
    public void Configure(EntityTypeBuilder<TripProvidedItem> builder)
    {
        builder.HasKey(e => e.TripProvidedItemId).HasName("trip_provided_items_pkey");

        builder.ToTable("trip_provided_items");

        builder.HasIndex(e => e.PreparedByUserId, "ix_trip_provided_items_prepared_by_user_id");
        builder.HasIndex(e => e.ProductVariantId, "ix_trip_provided_items_product_variant_id");
        builder.HasIndex(e => e.TourProvidedItemId, "ix_trip_provided_items_tour_provided_item_id");
        builder.HasIndex(e => e.TripId, "ix_trip_provided_items_trip_id");

        builder.Property(e => e.TripProvidedItemId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("trip_provided_item_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.PreparedByUserId).HasColumnName("prepared_by_user_id");
        builder.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(e => e.RequiredQuantity).HasColumnName("required_quantity");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.TourProvidedItemId).HasColumnName("tour_provided_item_id");
        builder.Property(e => e.TripId).HasColumnName("trip_id");

        builder.HasOne(d => d.PreparedByUser).WithMany(p => p.TripProvidedItems)
            .HasForeignKey(d => d.PreparedByUserId)
            .HasConstraintName("trip_provided_items_prepared_by_user_id_fkey");

        builder.HasOne(d => d.ProductVariant).WithMany(p => p.TripProvidedItems)
            .HasForeignKey(d => d.ProductVariantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("trip_provided_items_product_variant_id_fkey");

        builder.HasOne(d => d.TourProvidedItem).WithMany(p => p.TripProvidedItems)
            .HasForeignKey(d => d.TourProvidedItemId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("trip_provided_items_tour_provided_item_id_fkey");

        builder.HasOne(d => d.Trip).WithMany(p => p.TripProvidedItems)
            .HasForeignKey(d => d.TripId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("trip_provided_items_trip_id_fkey");
    }
}
