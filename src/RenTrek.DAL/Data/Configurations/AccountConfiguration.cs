using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

// Tên cột snake_case do AppDbContext tự chuyển; chỉ khai báo chỗ khác quy ước.
/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể account.
/// </summary>
public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(u => u.UserId).HasName("accounts_pkey");
        builder.Property(u => u.Email).IsRequired().HasMaxLength(255);
        builder.Property(u => u.PasswordHash).HasMaxLength(255);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(255);
        builder.Property(u => u.Phone).HasMaxLength(20);
        builder.Property(u => u.Status).IsRequired().HasMaxLength(50);
        builder.HasQueryFilter(u => !u.IsDeleted);

        builder.Property(x => x.UserId).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.Email, "accounts_email_key").IsUnique();
        builder.HasIndex(x => x.RoleId, "ix_accounts_role_id");
        builder.HasOne(x => x.Role)
            .WithMany(x => x.Accounts)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("accounts_role_id_fkey");
    }
}
