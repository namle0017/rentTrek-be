using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể participant invitation.
/// </summary>
public sealed class ParticipantInvitationConfiguration : IEntityTypeConfiguration<ParticipantInvitation>
{
    public void Configure(EntityTypeBuilder<ParticipantInvitation> builder)
    {
        builder.ToTable("participant_invitations");
        builder.HasKey(x => x.ParticipantInvitationId);
        builder.Property(x => x.ParticipantInvitationId).HasColumnName("participant_invitation_id");
        builder.Property(x => x.ParticipantId).HasColumnName("participant_id");
        builder.Property(x => x.BookingOrderId).HasColumnName("booking_order_id");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
        builder.Property(x => x.FailedAttempts).HasColumnName("failed_attempts");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.RedeemedAt).HasColumnName("redeemed_at");
        builder.Property(x => x.RedeemedByUserId).HasColumnName("redeemed_by_user_id");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.ParticipantId)
            .IsUnique()
            .HasFilter("redeemed_at IS NULL AND revoked_at IS NULL");
        builder.HasOne(x => x.Participant).WithMany(x => x.Invitations).HasForeignKey(x => x.ParticipantId);
        builder.HasOne(x => x.BookingOrder).WithMany(x => x.Invitations).HasForeignKey(x => x.BookingOrderId);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.RedeemedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_participant_invitations_expiry",
                "expires_at > created_at");
            table.HasCheckConstraint(
                "ck_participant_invitations_redemption",
                "(redeemed_at IS NULL AND redeemed_by_user_id IS NULL) OR " +
                "(redeemed_at IS NOT NULL AND redeemed_by_user_id IS NOT NULL)");
        });
    }
}
