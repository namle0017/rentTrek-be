using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.HasKey(e => e.ProfileId).HasName("profiles_pkey");

        builder.ToTable("profiles");

        builder.HasIndex(e => e.UserCode, "profiles_user_code_key").IsUnique();
        builder.HasIndex(e => e.UserId, "profiles_user_id_key").IsUnique();

        builder.Property(e => e.ProfileId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("profile_id");
        builder.Property(e => e.Address)
            .HasMaxLength(500)
            .HasColumnName("address");
        builder.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
        builder.Property(e => e.Bio).HasColumnName("bio");
        builder.Property(e => e.CareerStartDate).HasColumnName("career_start_date");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
        builder.Property(e => e.UserCode)
            .HasMaxLength(50)
            .HasColumnName("user_code");
        builder.Property(e => e.UserId).HasColumnName("user_id");

        builder.HasOne(d => d.User).WithOne(p => p.Profile)
            .HasForeignKey<Profile>(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("profiles_user_id_fkey");
    }
}
