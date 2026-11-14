using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho bảng rental_policies.
/// </summary>
public class RentalPolicyConfiguration : IEntityTypeConfiguration<RentalPolicy>
{
    public void Configure(EntityTypeBuilder<RentalPolicy> builder)
    {
        builder.HasKey(e => e.RentalPolicyId).HasName("rental_policies_pkey");
        builder.ToTable("rental_policies", table =>
        {
            table.HasCheckConstraint("rental_policies_deposit_percent_check", "((deposit_percent >= (0)::numeric) AND (deposit_percent <= (100)::numeric))");
            table.HasCheckConstraint("rental_policies_over_limit_cancel_fee_percent_check", "((over_limit_cancel_fee_percent >= (0)::numeric) AND (over_limit_cancel_fee_percent <= (100)::numeric))");
        });

        builder.HasIndex(e => new { e.PolicyFamilyId, e.Version }, "ux_rental_policies_family_version").IsUnique();

        builder.Property(e => e.RentalPolicyId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("rental_policy_id");
        builder.Property(e => e.PolicyFamilyId).HasColumnName("policy_family_id");
        builder.Property(e => e.Version).HasDefaultValue(1).HasColumnName("version");
        builder.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
        builder.Property(e => e.Status).HasMaxLength(30).HasColumnName("status");
        builder.Property(e => e.DepositPercent).HasPrecision(5, 2).HasColumnName("deposit_percent");
        builder.Property(e => e.FreeCancelLimit).HasDefaultValue(2).HasColumnName("free_cancel_limit");
        builder.Property(e => e.FreeCancelWindowDays).HasDefaultValue(30).HasColumnName("free_cancel_window_days");
        builder.Property(e => e.OverLimitCancelFeePercent).HasPrecision(5, 2).HasDefaultValue(5m).HasColumnName("over_limit_cancel_fee_percent");
        builder.Property(e => e.ReturnGraceHours).HasDefaultValue(2).HasColumnName("return_grace_hours");
        builder.Property(e => e.ExtensionMaxDaysLate).HasDefaultValue(2).HasColumnName("extension_max_days_late");
        builder.Property(e => e.SuspectedLossAfterDays).HasDefaultValue(7).HasColumnName("suspected_loss_after_days");
        builder.Property(e => e.ReceiptAutoConfirmHours).HasDefaultValue(24).HasColumnName("receipt_auto_confirm_hours");
        builder.Property(e => e.MinPhotosPerAsset).HasDefaultValue(1).HasColumnName("min_photos_per_asset");
        builder.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(e => e.EffectiveUntil).HasColumnName("effective_until");
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        builder.Property(e => e.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");

        builder.HasOne<Account>().WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .HasConstraintName("rental_policies_created_by_user_id_fkey");
    }
}
