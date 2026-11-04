using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RefundPolicyRuleConfiguration : IEntityTypeConfiguration<RefundPolicyRule>
{
    public void Configure(EntityTypeBuilder<RefundPolicyRule> builder)
    {
        builder.HasKey(e => e.RefundPolicyRuleId).HasName("refund_policy_rules_pkey");

        builder.ToTable("refund_policy_rules", table =>
        {
            table.HasCheckConstraint("refund_policy_rules_refund_percentage_check", "((refund_percentage >= (0)::numeric) AND (refund_percentage <= (100)::numeric))");
        });

        builder.HasIndex(e => e.RefundPolicyId, "ix_refund_policy_rules_refund_policy_id");

        builder.Property(e => e.RefundPolicyRuleId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("refund_policy_rule_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.MinHoursBefore).HasColumnName("min_hours_before");
        builder.Property(e => e.RefundPercentage)
            .HasPrecision(5, 2)
            .HasColumnName("refund_percentage");
        builder.Property(e => e.RefundPolicyId).HasColumnName("refund_policy_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.RefundPolicy).WithMany(p => p.RefundPolicyRules)
            .HasForeignKey(d => d.RefundPolicyId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("refund_policy_rules_refund_policy_id_fkey");
    }
}
