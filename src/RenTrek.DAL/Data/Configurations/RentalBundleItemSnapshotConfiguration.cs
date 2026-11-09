using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RentalBundleItemSnapshotConfiguration : IEntityTypeConfiguration<RentalBundleItemSnapshot>
{
    public void Configure(EntityTypeBuilder<RentalBundleItemSnapshot> builder)
    {
        builder.HasKey(e => e.RentalBundleItemSnapshotId).HasName("rental_bundle_item_snapshots_pkey");

        builder.ToTable("rental_bundle_item_snapshots", table =>
        {
            table.HasCheckConstraint("rental_bundle_item_snapshots_quantity_per_bundle_check", "(quantity_per_bundle > 0)");
        });

        builder.HasIndex(e => e.ProductVariantId, "ix_rental_bundle_item_snapshots_product_variant_id");
        builder.HasIndex(e => e.RentalOrderItemId, "ix_rental_bundle_item_snapshots_rental_order_item_id");

        builder.Property(e => e.RentalBundleItemSnapshotId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("rental_bundle_item_snapshot_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(e => e.QuantityPerBundle).HasColumnName("quantity_per_bundle");
        builder.Property(e => e.RentalOrderItemId).HasColumnName("rental_order_item_id");

        builder.HasOne(d => d.ProductVariant).WithMany(p => p.RentalBundleItemSnapshots)
            .HasForeignKey(d => d.ProductVariantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_bundle_item_snapshots_product_variant_id_fkey");

        builder.HasOne(d => d.RentalOrderItem).WithMany(p => p.RentalBundleItemSnapshots)
            .HasForeignKey(d => d.RentalOrderItemId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_bundle_item_snapshots_rental_order_item_id_fkey");
    }
}
