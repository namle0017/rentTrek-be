using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho OTP thao tác hoàn tiền.
/// </summary>
public sealed class RefundActionChallengeConfiguration : IEntityTypeConfiguration<RefundActionChallenge>
{
    public void Configure(EntityTypeBuilder<RefundActionChallenge> builder)
    {
        builder.ToTable("refund_action_challenges");
        builder.HasKey(x => x.ChallengeId).HasName("refund_action_challenges_pkey");
        builder.Property(x => x.ChallengeId).HasColumnName("challenge_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.AdminUserId).HasColumnName("admin_user_id");
        builder.Property(x => x.CodeHash).HasColumnName("code_hash").HasMaxLength(255);
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        builder.Property(x => x.UsedAt).HasColumnName("used_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.AdminUserId, x.CreatedAt }, "ix_refund_action_challenges_admin").IsDescending(false, true);
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("refund_action_challenges_admin_user_id_fkey");
    }
}
