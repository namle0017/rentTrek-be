using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RentalPriceTierConfiguration : IEntityTypeConfiguration<RentalPriceTier>
{
    public void Configure(EntityTypeBuilder<RentalPriceTier> builder)
    {
        builder.ToTable("RentalPriceTiers", table =>
        {
            table.HasCheckConstraint("CK_RentalPriceTiers_Days", "[MinDays] >= 1 AND ([MaxDays] IS NULL OR [MaxDays] >= [MinDays])");
            table.HasCheckConstraint("CK_RentalPriceTiers_DailyPrice", "[DailyPrice] >= 0");
        });
        builder.HasKey(tier => tier.Id);
        builder.Property(tier => tier.DailyPrice).HasPrecision(18, 0);
        builder.HasOne(tier => tier.ProductVariant).WithMany(variant => variant.RentalPriceTiers)
            .HasForeignKey(tier => tier.ProductVariantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(tier => !tier.IsDeleted && !tier.ProductVariant.IsDeleted
            && !tier.ProductVariant.Product.IsDeleted && !tier.ProductVariant.Product.Category.IsDeleted);
    }
}
