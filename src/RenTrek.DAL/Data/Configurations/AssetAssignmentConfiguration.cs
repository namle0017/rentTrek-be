using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    public void Configure(EntityTypeBuilder<AssetAssignment> builder)
    {
        builder.HasKey(e => e.AssetAssignmentId).HasName("asset_assignments_pkey");

        builder.ToTable("asset_assignments");

        builder.HasIndex(e => e.AppliedPromotionId, "ix_asset_assignments_applied_promotion_id");
        builder.HasIndex(e => e.EquipmentAssetId, "ix_asset_assignments_equipment_asset_id");
        builder.HasIndex(e => e.RentalOrderItemId, "ix_asset_assignments_rental_order_item_id");
        builder.HasIndex(e => e.TripProvidedItemId, "ix_asset_assignments_trip_provided_item_id");

        builder.Property(e => e.AssetAssignmentId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("asset_assignment_id");
        builder.Property(e => e.AppliedPromotionId).HasColumnName("applied_promotion_id");
        builder.Property(e => e.AssignedAt).HasColumnName("assigned_at");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.EquipmentAssetId).HasColumnName("equipment_asset_id");
        builder.Property(e => e.RentalOrderItemId).HasColumnName("rental_order_item_id");
        builder.Property(e => e.ReservedFrom).HasColumnName("reserved_from");
        builder.Property(e => e.ReservedUntil).HasColumnName("reserved_until");
        builder.Property(e => e.ReturnCondition)
            .HasMaxLength(255)
            .HasColumnName("return_condition");
        builder.Property(e => e.ReturnedAt).HasColumnName("returned_at");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.TripProvidedItemId).HasColumnName("trip_provided_item_id");

        builder.HasOne(d => d.AppliedPromotion).WithMany(p => p.AssetAssignments)
            .HasForeignKey(d => d.AppliedPromotionId)
            .HasConstraintName("asset_assignments_applied_promotion_id_fkey");

        builder.HasOne(d => d.EquipmentAsset).WithMany(p => p.AssetAssignments)
            .HasForeignKey(d => d.EquipmentAssetId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("asset_assignments_equipment_asset_id_fkey");

        builder.HasOne(d => d.RentalOrderItem).WithMany(p => p.AssetAssignments)
            .HasForeignKey(d => d.RentalOrderItemId)
            .HasConstraintName("asset_assignments_rental_order_item_id_fkey");

        builder.HasOne(d => d.TripProvidedItem).WithMany(p => p.AssetAssignments)
            .HasForeignKey(d => d.TripProvidedItemId)
            .HasConstraintName("asset_assignments_trip_provided_item_id_fkey");
    }
}
