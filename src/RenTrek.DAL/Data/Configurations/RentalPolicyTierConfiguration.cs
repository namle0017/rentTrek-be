using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho bảng rental_policy_tiers.
/// </summary>
public class RentalPolicyTierConfiguration : IEntityTypeConfiguration<RentalPolicyTier>
{
    public void Configure(EntityTypeBuilder<RentalPolicyTier> builder)
    {
        builder.HasKey(e => e.RentalPolicyTierId).HasName("rental_policy_tiers_pkey");
        builder.ToTable("rental_policy_tiers", table =>
        {
            table.HasCheckConstraint("rental_policy_tiers_scope_check", "((scope)::text = ANY ((ARRAY['PickupLate'::character varying, 'ReturnLate'::character varying])::text[]))");
            table.HasCheckConstraint("rental_policy_tiers_min_days_late_check", "(min_days_late > 0)");
        });

        builder.HasIndex(e => new { e.RentalPolicyId, e.Scope, e.MinDaysLate }, "ux_rental_policy_tiers_scope_days").IsUnique();

        builder.Property(e => e.RentalPolicyTierId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("rental_policy_tier_id");
        builder.Property(e => e.RentalPolicyId).HasColumnName("rental_policy_id");
        builder.Property(e => e.Scope).HasMaxLength(30).HasColumnName("scope");
        builder.Property(e => e.MinDaysLate).HasColumnName("min_days_late");
        builder.Property(e => e.PercentOfDeposit).HasPrecision(5, 2).HasColumnName("percent_of_deposit");
        builder.Property(e => e.IsTerminal).HasDefaultValue(false).HasColumnName("is_terminal");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

        builder.HasOne(d => d.RentalPolicy).WithMany(p => p.Tiers)
            .HasForeignKey(d => d.RentalPolicyId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_policy_tiers_rental_policy_id_fkey");
    }
}
