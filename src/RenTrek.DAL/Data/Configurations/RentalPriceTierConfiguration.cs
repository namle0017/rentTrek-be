using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể rental price tier.
/// </summary>
public class RentalPriceTierConfiguration : IEntityTypeConfiguration<RentalPriceTier>
{
    public void Configure(EntityTypeBuilder<RentalPriceTier> builder)
    {
        builder.ToTable("rental_price_tiers", table =>
        {
            table.HasCheckConstraint("rental_price_tiers_check", "((product_variant_id IS NULL) <> (bundle_id IS NULL))");
        });
        builder.HasKey(tier => tier.Id).HasName("rental_price_tiers_pkey");
        builder.Property(tier => tier.Id).HasColumnName("price_tier_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(tier => tier.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(tier => tier.BundleId).HasColumnName("bundle_id");
        builder.Property(tier => tier.DailyPrice).HasPrecision(18, 0);
        builder.HasQueryFilter(tier => !tier.IsDeleted && tier.ProductVariant != null && !tier.ProductVariant.IsDeleted
            && !tier.ProductVariant.Product.IsDeleted && !tier.ProductVariant.Product.Category.IsDeleted);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.BundleId, "ix_rental_price_tiers_bundle_id");
        builder.HasIndex(x => x.ProductVariantId, "ix_rental_price_tiers_product_variant_id");
        builder.HasOne(x => x.Bundle)
            .WithMany(x => x.RentalPriceTiers)
            .HasForeignKey(x => x.BundleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("rental_price_tiers_bundle_id_fkey");
        builder.HasOne(x => x.ProductVariant)
            .WithMany(x => x.RentalPriceTiers)
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("rental_price_tiers_product_variant_id_fkey");
    }
}
