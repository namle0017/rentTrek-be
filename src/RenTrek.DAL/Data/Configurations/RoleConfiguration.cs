using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(e => e.RoleId).HasName("roles_pkey");

        builder.ToTable("roles");

        builder.HasIndex(e => e.RoleName, "roles_role_name_key").IsUnique();

        builder.Property(e => e.RoleId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("role_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.RoleName)
            .HasMaxLength(50)
            .HasColumnName("role_name");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
    }
}
