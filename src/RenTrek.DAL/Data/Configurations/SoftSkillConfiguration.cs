using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class SoftSkillConfiguration : IEntityTypeConfiguration<SoftSkill>
{
    public void Configure(EntityTypeBuilder<SoftSkill> builder)
    {
        builder.HasKey(e => e.SoftSkillId).HasName("soft_skills_pkey");

        builder.ToTable("soft_skills");

        builder.HasIndex(e => e.Name, "soft_skills_name_key").IsUnique();

        builder.Property(e => e.SoftSkillId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("soft_skill_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.Name)
            .HasMaxLength(100)
            .HasColumnName("name");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
    }
}
