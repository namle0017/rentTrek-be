using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class AccessTokenConfiguration : IEntityTypeConfiguration<AccessToken>
{
    public void Configure(EntityTypeBuilder<AccessToken> builder)
    {
        builder.ToTable("AccessTokens");

        builder.HasKey(at => at.AccessTokenId);

        builder.Property(at => at.Jti)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(at => at.Jti)
            .IsUnique();

        builder.Property(at => at.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(at => at.RevokedReason)
            .HasMaxLength(255);

        builder.HasOne(at => at.User)
            .WithMany(u => u.AccessTokens)
            .HasForeignKey(at => at.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(at => at.RefreshToken)
            .WithMany(rt => rt.AccessTokens)
            .HasForeignKey(at => at.RefreshTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
