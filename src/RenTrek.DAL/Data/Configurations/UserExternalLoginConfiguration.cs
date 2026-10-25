using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> builder)
    {
        builder.HasKey(e => e.UserExternalLoginId).HasName("user_external_logins_pkey");

        builder.ToTable("user_external_logins");

        builder.HasIndex(e => e.UserId, "ix_user_external_logins_user_id");
        builder.HasIndex(e => new { e.Provider, e.ProviderSubject }, "user_external_logins_provider_provider_subject_key").IsUnique();

        builder.Property(e => e.UserExternalLoginId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("user_external_login_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.Provider)
            .HasMaxLength(50)
            .HasColumnName("provider");
        builder.Property(e => e.ProviderSubject)
            .HasMaxLength(255)
            .HasColumnName("provider_subject");
        builder.Property(e => e.UserId).HasColumnName("user_id");

        builder.HasOne(d => d.User).WithMany(p => p.UserExternalLogins)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("user_external_logins_user_id_fkey");
    }
}
