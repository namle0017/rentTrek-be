using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

// Tên cột snake_case do AppDbContext tự chuyển; chỉ khai báo chỗ khác quy ước.
public class AccessTokenConfiguration : IEntityTypeConfiguration<AccessToken>
{
    public void Configure(EntityTypeBuilder<AccessToken> builder)
    {
        builder.ToTable("access_tokens");
        builder.HasKey(at => at.AccessTokenId);
        builder.Property(at => at.Jti).IsRequired().HasMaxLength(255);
        builder.HasIndex(at => at.Jti).IsUnique();
        builder.Property(at => at.RevokedReason).HasMaxLength(255);
        builder.HasOne(at => at.Account)
            .WithMany(a => a.AccessTokens)
            .HasForeignKey(at => at.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(at => at.RefreshToken)
            .WithMany(rt => rt.AccessTokens)
            .HasForeignKey(at => at.RefreshTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
