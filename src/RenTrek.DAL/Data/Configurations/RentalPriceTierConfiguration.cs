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
        builder.ToTable("rental_price_tiers");
        builder.HasKey(tier => tier.Id);
        builder.Property(tier => tier.Id).HasColumnName("price_tier_id");
        builder.Property(tier => tier.DailyPrice).HasPrecision(18, 0);
        builder.HasOne(tier => tier.ProductVariant).WithMany(variant => variant.RentalPriceTiers)
            .HasForeignKey(tier => tier.ProductVariantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(tier => !tier.IsDeleted && !tier.ProductVariant.IsDeleted
            && !tier.ProductVariant.Product.IsDeleted && !tier.ProductVariant.Product.Category.IsDeleted);
    }
}
