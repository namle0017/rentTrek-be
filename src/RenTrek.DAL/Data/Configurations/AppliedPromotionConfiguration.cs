using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class AppliedPromotionConfiguration : IEntityTypeConfiguration<AppliedPromotion>
{
    public void Configure(EntityTypeBuilder<AppliedPromotion> builder)
    {
        builder.HasKey(e => e.AppliedPromotionId).HasName("applied_promotions_pkey");

        builder.ToTable("applied_promotions");

        builder.HasIndex(e => e.GiftProductVariantId, "ix_applied_promotions_gift_product_variant_id");
        builder.HasIndex(e => e.OrderId, "ix_applied_promotions_order_id");
        builder.HasIndex(e => e.PromotionTierId, "ix_applied_promotions_promotion_tier_id");
        builder.HasIndex(e => e.RentalOrderItemId, "ix_applied_promotions_rental_order_item_id");

        builder.Property(e => e.AppliedPromotionId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("applied_promotion_id");
        builder.Property(e => e.AppliedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("applied_at");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.DiscountAmount)
            .HasPrecision(18)
            .HasColumnName("discount_amount");
        builder.Property(e => e.GiftProductVariantId).HasColumnName("gift_product_variant_id");
        builder.Property(e => e.GiftQuantity).HasColumnName("gift_quantity");
        builder.Property(e => e.OrderId).HasColumnName("order_id");
        builder.Property(e => e.PromotionTierId).HasColumnName("promotion_tier_id");
        builder.Property(e => e.RentalOrderItemId).HasColumnName("rental_order_item_id");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");

        builder.HasOne(d => d.GiftProductVariant).WithMany(p => p.AppliedPromotions)
            .HasForeignKey(d => d.GiftProductVariantId)
            .HasConstraintName("applied_promotions_gift_product_variant_id_fkey");

        builder.HasOne(d => d.Order).WithMany(p => p.AppliedPromotions)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("applied_promotions_order_id_fkey");

        builder.HasOne(d => d.PromotionTier).WithMany(p => p.AppliedPromotions)
            .HasForeignKey(d => d.PromotionTierId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("applied_promotions_promotion_tier_id_fkey");

        builder.HasOne(d => d.RentalOrderItem).WithMany(p => p.AppliedPromotions)
            .HasForeignKey(d => d.RentalOrderItemId)
            .HasConstraintName("applied_promotions_rental_order_item_id_fkey");
    }
}
