using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RefundPolicyConfiguration : IEntityTypeConfiguration<RefundPolicy>
{
    public void Configure(EntityTypeBuilder<RefundPolicy> builder)
    {
        builder.HasKey(e => e.RefundPolicyId).HasName("refund_policies_pkey");

        builder.ToTable("refund_policies");

        builder.HasIndex(e => e.CreatedByUserId, "ix_refund_policies_created_by_user_id");
        builder.HasIndex(e => new { e.PolicyFamilyId, e.Version }, "refund_policies_policy_family_id_version_key").IsUnique();

        builder.Property(e => e.RefundPolicyId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("refund_policy_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(e => e.EffectiveUntil).HasColumnName("effective_until");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.Name)
            .HasMaxLength(255)
            .HasColumnName("name");
        builder.Property(e => e.PolicyFamilyId).HasColumnName("policy_family_id");
        builder.Property(e => e.PolicyType)
            .HasMaxLength(50)
            .HasColumnName("policy_type");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
        builder.Property(e => e.Version)
            .HasDefaultValue(1)
            .HasColumnName("version");

        builder.HasOne(d => d.CreatedByUser).WithMany(p => p.RefundPolicies)
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("refund_policies_created_by_user_id_fkey");
    }
}
