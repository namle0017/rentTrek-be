using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

// Tên cột snake_case do AppDbContext tự chuyển; chỉ khai báo chỗ khác quy ước.
/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể access token.
/// </summary>
public class AccessTokenConfiguration : IEntityTypeConfiguration<AccessToken>
{
    public void Configure(EntityTypeBuilder<AccessToken> builder)
    {
        builder.ToTable("access_tokens");
        builder.HasKey(at => at.AccessTokenId).HasName("access_tokens_pkey");
        builder.Property(at => at.Jti).IsRequired().HasMaxLength(255);
        builder.Property(at => at.RevokedReason).HasMaxLength(255);

        builder.Property(x => x.AccessTokenId).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.Jti, "access_tokens_jti_key").IsUnique();
        builder.HasIndex(x => x.RefreshTokenId, "ix_access_tokens_refresh_token_id");
        builder.HasIndex(x => x.UserId, "ix_access_tokens_user_id");
        builder.HasOne(x => x.RefreshToken)
            .WithMany(x => x.AccessTokens)
            .HasForeignKey(x => x.RefreshTokenId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("access_tokens_refresh_token_id_fkey");
        builder.HasOne(x => x.Account)
            .WithMany(x => x.AccessTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("access_tokens_user_id_fkey");
    }
}
