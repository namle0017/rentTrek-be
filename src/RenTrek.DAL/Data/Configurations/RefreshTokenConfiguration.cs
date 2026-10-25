using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

// Tên cột snake_case do AppDbContext tự chuyển; chỉ khai báo chỗ khác quy ước.
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(rt => rt.RefreshTokenId);
        builder.HasIndex(rt => rt.FamilyId);
        builder.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(255);
        builder.HasIndex(rt => rt.TokenHash).IsUnique();
        builder.Property(rt => rt.RevokedReason).HasMaxLength(255);
        builder.HasOne(rt => rt.Account)
            .WithMany(a => a.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(rt => rt.ReplacedByRefreshToken)
            .WithMany()
            .HasForeignKey(rt => rt.ReplacedByRefreshTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
