using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.HasKey(e => e.PasswordResetTokenId).HasName("password_reset_tokens_pkey");

        builder.ToTable("password_reset_tokens");

        builder.HasIndex(e => e.UserId, "ix_password_reset_tokens_user_id");
        builder.HasIndex(e => e.TokenHash, "password_reset_tokens_token_hash_key").IsUnique();

        builder.Property(e => e.PasswordResetTokenId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("password_reset_token_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.TokenHash)
            .HasMaxLength(255)
            .HasColumnName("token_hash");
        builder.Property(e => e.UsedAt).HasColumnName("used_at");
        builder.Property(e => e.UserId).HasColumnName("user_id");

        builder.HasOne(d => d.User).WithMany(p => p.PasswordResetTokens)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("password_reset_tokens_user_id_fkey");
    }
}
