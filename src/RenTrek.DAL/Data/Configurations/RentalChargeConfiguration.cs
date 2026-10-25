using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RentalChargeConfiguration : IEntityTypeConfiguration<RentalCharge>
{
    public void Configure(EntityTypeBuilder<RentalCharge> builder)
    {
        builder.HasKey(e => e.RentalChargeId).HasName("rental_charges_pkey");

        builder.ToTable("rental_charges");

        builder.HasIndex(e => e.CreatedByUserId, "ix_rental_charges_created_by_user_id");
        builder.HasIndex(e => e.EquipmentAssetId, "ix_rental_charges_equipment_asset_id");
        builder.HasIndex(e => e.RentalOrderId, "ix_rental_charges_rental_order_id");

        builder.Property(e => e.RentalChargeId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("rental_charge_id");
        builder.Property(e => e.Amount)
            .HasPrecision(18)
            .HasColumnName("amount");
        builder.Property(e => e.ChargeType)
            .HasMaxLength(50)
            .HasColumnName("charge_type");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.EquipmentAssetId).HasColumnName("equipment_asset_id");
        builder.Property(e => e.Reason)
            .HasMaxLength(500)
            .HasColumnName("reason");
        builder.Property(e => e.RentalOrderId).HasColumnName("rental_order_id");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.VoidedAt).HasColumnName("voided_at");

        builder.HasOne(d => d.CreatedByUser).WithMany(p => p.RentalCharges)
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_charges_created_by_user_id_fkey");

        builder.HasOne(d => d.EquipmentAsset).WithMany(p => p.RentalCharges)
            .HasForeignKey(d => d.EquipmentAssetId)
            .HasConstraintName("rental_charges_equipment_asset_id_fkey");

        builder.HasOne(d => d.RentalOrder).WithMany(p => p.RentalCharges)
            .HasForeignKey(d => d.RentalOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_charges_rental_order_id_fkey");
    }
}
