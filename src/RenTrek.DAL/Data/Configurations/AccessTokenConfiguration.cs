using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class AccessTokenConfiguration : IEntityTypeConfiguration<AccessToken>
{
    public void Configure(EntityTypeBuilder<AccessToken> builder)
    {
        builder.HasKey(e => e.AccessTokenId).HasName("access_tokens_pkey");

        builder.ToTable("access_tokens");

        builder.HasIndex(e => e.Jti, "access_tokens_jti_key").IsUnique();
        builder.HasIndex(e => e.RefreshTokenId, "ix_access_tokens_refresh_token_id");
        builder.HasIndex(e => e.UserId, "ix_access_tokens_user_id");

        builder.Property(e => e.AccessTokenId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("access_token_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.Jti)
            .HasMaxLength(255)
            .HasColumnName("jti");
        builder.Property(e => e.RefreshTokenId).HasColumnName("refresh_token_id");
        builder.Property(e => e.RevokedAt).HasColumnName("revoked_at");
        builder.Property(e => e.RevokedReason)
            .HasMaxLength(255)
            .HasColumnName("revoked_reason");
        builder.Property(e => e.UserId).HasColumnName("user_id");

        builder.HasOne(d => d.RefreshToken).WithMany(p => p.AccessTokens)
            .HasForeignKey(d => d.RefreshTokenId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("access_tokens_refresh_token_id_fkey");

        builder.HasOne(d => d.User).WithMany(p => p.AccessTokens)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("access_tokens_user_id_fkey");
    }
}
