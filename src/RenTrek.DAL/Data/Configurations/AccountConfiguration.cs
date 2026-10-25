using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(e => e.UserId).HasName("accounts_pkey");

        builder.ToTable("accounts");

        builder.HasIndex(e => e.Email, "accounts_email_key").IsUnique();
        builder.HasIndex(e => e.RoleId, "ix_accounts_role_id");

        builder.Property(e => e.UserId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("user_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.Email)
            .HasMaxLength(255)
            .HasColumnName("email");
        builder.Property(e => e.EmailConfirmedAt).HasColumnName("email_confirmed_at");
        builder.Property(e => e.FullName)
            .HasMaxLength(255)
            .HasColumnName("full_name");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(e => e.PasswordHash)
            .HasMaxLength(255)
            .HasColumnName("password_hash");
        builder.Property(e => e.Phone)
            .HasMaxLength(20)
            .HasColumnName("phone");
        builder.Property(e => e.RoleId).HasColumnName("role_id");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Role).WithMany(p => p.Accounts)
            .HasForeignKey(d => d.RoleId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("accounts_role_id_fkey");
    }
}
