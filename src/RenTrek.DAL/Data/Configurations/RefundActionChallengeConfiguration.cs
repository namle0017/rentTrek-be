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
        builder.HasKey(x => x.ChallengeId);
        builder.Property(x => x.ChallengeId).HasColumnName("challenge_id");
        builder.Property(x => x.AdminUserId).HasColumnName("admin_user_id");
        builder.Property(x => x.CodeHash).HasColumnName("code_hash").HasMaxLength(255);
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.UsedAt).HasColumnName("used_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}
