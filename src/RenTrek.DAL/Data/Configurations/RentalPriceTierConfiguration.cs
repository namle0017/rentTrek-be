using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RentalPriceTierConfiguration : IEntityTypeConfiguration<RentalPriceTier>
{
    public void Configure(EntityTypeBuilder<RentalPriceTier> builder)
    {
        builder.HasKey(e => e.PriceTierId).HasName("rental_price_tiers_pkey");

        builder.ToTable("rental_price_tiers");

        builder.HasIndex(e => e.BundleId, "ix_rental_price_tiers_bundle_id");
        builder.HasIndex(e => e.ProductVariantId, "ix_rental_price_tiers_product_variant_id");

        builder.Property(e => e.PriceTierId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("price_tier_id");
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
        builder.Property(e => e.MaxDays).HasColumnName("max_days");
        builder.Property(e => e.MinDays).HasColumnName("min_days");
        builder.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Bundle).WithMany(p => p.RentalPriceTiers)
            .HasForeignKey(d => d.BundleId)
            .HasConstraintName("rental_price_tiers_bundle_id_fkey");

        builder.HasOne(d => d.ProductVariant).WithMany(p => p.RentalPriceTiers)
            .HasForeignKey(d => d.ProductVariantId)
            .HasConstraintName("rental_price_tiers_product_variant_id_fkey");
    }
}
