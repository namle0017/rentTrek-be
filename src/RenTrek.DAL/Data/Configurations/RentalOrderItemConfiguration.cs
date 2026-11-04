using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RentalOrderItemConfiguration : IEntityTypeConfiguration<RentalOrderItem>
{
    public void Configure(EntityTypeBuilder<RentalOrderItem> builder)
    {
        builder.HasKey(e => e.RentalOrderItemId).HasName("rental_order_items_pkey");

        builder.ToTable("rental_order_items", table =>
        {
            table.HasCheckConstraint("rental_order_items_check", "((product_variant_id IS NULL) <> (bundle_id IS NULL))");
            table.HasCheckConstraint("rental_order_items_quantity_check", "(quantity > 0)");
        });

        builder.HasIndex(e => e.BundleId, "ix_rental_order_items_bundle_id");
        builder.HasIndex(e => e.ProductVariantId, "ix_rental_order_items_product_variant_id");
        builder.HasIndex(e => e.RentalOrderId, "ix_rental_order_items_rental_order_id");

        builder.Property(e => e.RentalOrderItemId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("rental_order_item_id");
        builder.Property(e => e.BillableDays).HasColumnName("billable_days");
        builder.Property(e => e.BundleId).HasColumnName("bundle_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.DailyPrice)
            .HasPrecision(18)
            .HasColumnName("daily_price");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.LineAmount)
            .HasPrecision(18)
            .HasColumnName("line_amount");
        builder.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(e => e.Quantity).HasColumnName("quantity");
        builder.Property(e => e.RentalOrderId).HasColumnName("rental_order_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Bundle).WithMany(p => p.RentalOrderItems)
            .HasForeignKey(d => d.BundleId)
            .HasConstraintName("rental_order_items_bundle_id_fkey");

        builder.HasOne(d => d.ProductVariant).WithMany(p => p.RentalOrderItems)
            .HasForeignKey(d => d.ProductVariantId)
            .HasConstraintName("rental_order_items_product_variant_id_fkey");

        builder.HasOne(d => d.RentalOrder).WithMany(p => p.RentalOrderItems)
            .HasForeignKey(d => d.RentalOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_order_items_rental_order_id_fkey");
    }
}
