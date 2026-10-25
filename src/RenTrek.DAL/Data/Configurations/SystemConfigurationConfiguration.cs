using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> builder)
    {
        builder.HasKey(e => e.ConfigurationId).HasName("system_configurations_pkey");

        builder.ToTable("system_configurations");

        builder.HasIndex(e => e.UpdatedByUserId, "ix_system_configurations_updated_by_user_id");
        builder.HasIndex(e => e.ConfigKey, "system_configurations_config_key_key").IsUnique();

        builder.Property(e => e.ConfigurationId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("configuration_id");
        builder.Property(e => e.ConfigKey)
            .HasMaxLength(100)
            .HasColumnName("config_key");
        builder.Property(e => e.ConfigValue)
            .HasMaxLength(2000)
            .HasColumnName("config_value");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
        builder.Property(e => e.UpdatedByUserId).HasColumnName("updated_by_user_id");
        builder.Property(e => e.ValueType)
            .HasMaxLength(50)
            .HasColumnName("value_type");

        builder.HasOne(d => d.UpdatedByUser).WithMany(p => p.SystemConfigurations)
            .HasForeignKey(d => d.UpdatedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("system_configurations_updated_by_user_id_fkey");
    }
}
