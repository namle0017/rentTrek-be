using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

// Tên cột snake_case do AppDbContext tự chuyển; chỉ khai báo chỗ khác quy ước.
/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể role.
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.RoleId).HasName("roles_pkey");
        builder.Property(r => r.RoleName).IsRequired().HasMaxLength(50);
        builder.HasQueryFilter(r => !r.IsDeleted);

        builder.Property(x => x.RoleId).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.RoleName, "roles_role_name_key").IsUnique();
    }
}
