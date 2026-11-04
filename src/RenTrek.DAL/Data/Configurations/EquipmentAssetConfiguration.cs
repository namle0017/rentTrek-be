using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class EquipmentAssetConfiguration : IEntityTypeConfiguration<EquipmentAsset>
{
    public void Configure(EntityTypeBuilder<EquipmentAsset> builder)
    {
        builder.HasKey(e => e.EquipmentAssetId).HasName("equipment_assets_pkey");

        builder.ToTable("equipment_assets");

        builder.HasIndex(e => e.AssetCode, "equipment_assets_asset_code_key").IsUnique();
        builder.HasIndex(e => e.ProductVariantId, "ix_equipment_assets_product_variant_id");

        builder.Property(e => e.EquipmentAssetId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("equipment_asset_id");
        builder.Property(e => e.AssetCode)
            .HasMaxLength(100)
            .HasColumnName("asset_code");
        builder.Property(e => e.AssetStatus)
            .HasMaxLength(50)
            .HasColumnName("asset_status");
        builder.Property(e => e.ConditionNote).HasColumnName("condition_note");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.ProductVariant).WithMany(p => p.EquipmentAssets)
            .HasForeignKey(d => d.ProductVariantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("equipment_assets_product_variant_id_fkey");
    }
}
