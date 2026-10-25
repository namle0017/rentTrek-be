using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class PostSoftSkillConfiguration : IEntityTypeConfiguration<PostSoftSkill>
{
    public void Configure(EntityTypeBuilder<PostSoftSkill> builder)
    {
        builder.HasKey(e => e.PostSoftSkillId).HasName("post_soft_skills_pkey");

        builder.ToTable("post_soft_skills");

        builder.HasIndex(e => e.SoftSkillId, "ix_post_soft_skills_soft_skill_id");
        builder.HasIndex(e => new { e.PostId, e.SoftSkillId }, "post_soft_skills_post_id_soft_skill_id_key").IsUnique();

        builder.Property(e => e.PostSoftSkillId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("post_soft_skill_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.PostId).HasColumnName("post_id");
        builder.Property(e => e.SoftSkillId).HasColumnName("soft_skill_id");

        builder.HasOne(d => d.Post).WithMany(p => p.PostSoftSkills)
            .HasForeignKey(d => d.PostId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("post_soft_skills_post_id_fkey");

        builder.HasOne(d => d.SoftSkill).WithMany(p => p.PostSoftSkills)
            .HasForeignKey(d => d.SoftSkillId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("post_soft_skills_soft_skill_id_fkey");
    }
}
