using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class PostTagConfiguration : IEntityTypeConfiguration<PostTag>
{
    public void Configure(EntityTypeBuilder<PostTag> builder)
    {
        builder.HasKey(e => e.PostTagId).HasName("post_tags_pkey");

        builder.ToTable("post_tags");

        builder.HasIndex(e => e.TagId, "ix_post_tags_tag_id");
        builder.HasIndex(e => new { e.PostId, e.TagId }, "post_tags_post_id_tag_id_key").IsUnique();

        builder.Property(e => e.PostTagId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("post_tag_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.PostId).HasColumnName("post_id");
        builder.Property(e => e.TagId).HasColumnName("tag_id");

        builder.HasOne(d => d.Post).WithMany(p => p.PostTags)
            .HasForeignKey(d => d.PostId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("post_tags_post_id_fkey");

        builder.HasOne(d => d.Tag).WithMany(p => p.PostTags)
            .HasForeignKey(d => d.TagId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("post_tags_tag_id_fkey");
    }
}
