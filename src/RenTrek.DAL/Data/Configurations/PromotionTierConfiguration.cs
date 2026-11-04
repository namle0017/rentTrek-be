using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class PromotionTierConfiguration : IEntityTypeConfiguration<PromotionTier>
{
    public void Configure(EntityTypeBuilder<PromotionTier> builder)
    {
        builder.HasKey(e => e.PromotionTierId).HasName("promotion_tiers_pkey");

        builder.ToTable("promotion_tiers");

        builder.HasIndex(e => e.GiftProductVariantId, "ix_promotion_tiers_gift_product_variant_id");
        builder.HasIndex(e => e.PromotionId, "ix_promotion_tiers_promotion_id");

        builder.Property(e => e.PromotionTierId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("promotion_tier_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.DiscountType)
            .HasMaxLength(50)
            .HasColumnName("discount_type");
        builder.Property(e => e.DiscountValue)
            .HasPrecision(18, 2)
            .HasColumnName("discount_value");
        builder.Property(e => e.GiftProductVariantId).HasColumnName("gift_product_variant_id");
        builder.Property(e => e.GiftQuantity).HasColumnName("gift_quantity");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.MinThreshold)
            .HasPrecision(18)
            .HasColumnName("min_threshold");
        builder.Property(e => e.PromotionId).HasColumnName("promotion_id");
        builder.Property(e => e.QualificationMetric)
            .HasMaxLength(50)
            .HasColumnName("qualification_metric");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.GiftProductVariant).WithMany(p => p.PromotionTiers)
            .HasForeignKey(d => d.GiftProductVariantId)
            .HasConstraintName("promotion_tiers_gift_product_variant_id_fkey");

        builder.HasOne(d => d.Promotion).WithMany(p => p.PromotionTiers)
            .HasForeignKey(d => d.PromotionId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("promotion_tiers_promotion_id_fkey");
    }
}
