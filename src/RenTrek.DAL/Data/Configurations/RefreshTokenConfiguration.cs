using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.RefreshTokenId);

        builder.Property(rt => rt.FamilyId)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(rt => rt.FamilyId);

        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique();

        builder.Property(rt => rt.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(rt => rt.RevokedReason)
            .HasMaxLength(255);

        builder.Property(rt => rt.DeviceInfo)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(rt => rt.IpAddress)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rt => rt.ReplacedByRefreshToken)
            .WithMany()
            .HasForeignKey(rt => rt.ReplacedByRefreshTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
