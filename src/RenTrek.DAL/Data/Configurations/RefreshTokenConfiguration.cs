using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

// Tên cột snake_case do AppDbContext tự chuyển; chỉ khai báo chỗ khác quy ước.
/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể refresh token.
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(rt => rt.RefreshTokenId).HasName("refresh_tokens_pkey");
        builder.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(255);
        builder.Property(rt => rt.RevokedReason).HasMaxLength(255);

        builder.Property(x => x.RefreshTokenId).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.UserId, "ix_refresh_tokens_user_id");
        builder.HasIndex(x => x.ReplacedByRefreshTokenId, "refresh_tokens_replaced_by_refresh_token_id_key").IsUnique();
        builder.HasIndex(x => x.TokenHash, "refresh_tokens_token_hash_key").IsUnique();
        builder.HasOne(x => x.ReplacedByRefreshToken)
            .WithOne()
            .HasForeignKey<RefreshToken>(x => x.ReplacedByRefreshTokenId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("refresh_tokens_replaced_by_refresh_token_id_fkey");
        builder.HasOne(x => x.Account)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("refresh_tokens_user_id_fkey");
    }
}
