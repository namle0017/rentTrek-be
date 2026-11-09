using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Ánh xạ theo scaffold và ràng buộc của bảng public.participant_invitations.
/// </summary>
public sealed class ParticipantInvitationConfiguration : IEntityTypeConfiguration<ParticipantInvitation>
{
    public void Configure(EntityTypeBuilder<ParticipantInvitation> builder)
    {
        builder.ToTable("participant_invitations", table =>
        {
            table.HasCheckConstraint("ck_participant_invitations_expiry", "expires_at > created_at");
            table.HasCheckConstraint("ck_participant_invitations_failed_attempts", "failed_attempts >= 0");
            table.HasCheckConstraint("ck_participant_invitations_redemption",
                "((redeemed_at IS NULL) AND (redeemed_by_user_id IS NULL)) OR ((redeemed_at IS NOT NULL) AND (redeemed_by_user_id IS NOT NULL))");
            table.HasCheckConstraint("ck_participant_invitations_token_hash_length", "length(token_hash::text) = 64");
        });
        builder.HasKey(x => x.ParticipantInvitationId).HasName("participant_invitations_pkey");
        builder.Property(x => x.ParticipantInvitationId).ValueGeneratedNever().HasColumnName("participant_invitation_id");
        builder.Property(x => x.ParticipantId).HasColumnName("participant_id");
        builder.Property(x => x.BookingOrderId).HasColumnName("booking_order_id");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.RedeemedAt).HasColumnName("redeemed_at");
        builder.Property(x => x.RedeemedByUserId).HasColumnName("redeemed_by_user_id");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.FailedAttempts).HasColumnName("failed_attempts").HasDefaultValue(0);

        builder.HasIndex(x => x.BookingOrderId, "ix_participant_invitations_booking_order_id");
        builder.HasIndex(x => x.TokenHash, "uq_participant_invitations_token_hash").IsUnique();
        builder.HasIndex(x => x.ParticipantId, "ux_participant_invitations_active_participant")
            .IsUnique().HasFilter("((redeemed_at IS NULL) AND (revoked_at IS NULL))");

        builder.HasOne<TripBookingOrder>().WithMany().HasForeignKey(x => x.BookingOrderId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_participant_invitations_booking_order");
        builder.HasOne<BookingParticipant>().WithMany().HasForeignKey(x => x.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_participant_invitations_participant");
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_participant_invitations_created_by");
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.RedeemedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_participant_invitations_redeemed_by");
    }
}
