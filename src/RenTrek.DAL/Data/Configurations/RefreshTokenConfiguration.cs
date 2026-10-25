using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(e => e.RefreshTokenId).HasName("refresh_tokens_pkey");

        builder.ToTable("refresh_tokens");

        builder.HasIndex(e => e.UserId, "ix_refresh_tokens_user_id");
        builder.HasIndex(e => e.ReplacedByRefreshTokenId, "refresh_tokens_replaced_by_refresh_token_id_key").IsUnique();
        builder.HasIndex(e => e.TokenHash, "refresh_tokens_token_hash_key").IsUnique();

        builder.Property(e => e.RefreshTokenId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("refresh_token_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.FamilyId).HasColumnName("family_id");
        builder.Property(e => e.ReplacedByRefreshTokenId).HasColumnName("replaced_by_refresh_token_id");
        builder.Property(e => e.RevokedAt).HasColumnName("revoked_at");
        builder.Property(e => e.RevokedReason)
            .HasMaxLength(255)
            .HasColumnName("revoked_reason");
        builder.Property(e => e.TokenHash)
            .HasMaxLength(255)
            .HasColumnName("token_hash");
        builder.Property(e => e.UserId).HasColumnName("user_id");

        builder.HasOne(d => d.ReplacedByRefreshToken).WithOne(p => p.InverseReplacedByRefreshToken)
            .HasForeignKey<RefreshToken>(d => d.ReplacedByRefreshTokenId)
            .HasConstraintName("refresh_tokens_replaced_by_refresh_token_id_fkey");

        builder.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("refresh_tokens_user_id_fkey");
    }
}
