using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.HasKey(e => e.PostId).HasName("posts_pkey");

        builder.ToTable("posts");

        builder.HasIndex(e => e.CreatedByUserId, "ix_posts_created_by_user_id");
        builder.HasIndex(e => e.Slug, "posts_slug_key").IsUnique();

        builder.Property(e => e.PostId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("post_id");
        builder.Property(e => e.Content).HasColumnName("content");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.PostType)
            .HasMaxLength(50)
            .HasColumnName("post_type");
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.Slug)
            .HasMaxLength(255)
            .HasColumnName("slug");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.Title)
            .HasMaxLength(255)
            .HasColumnName("title");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.CreatedByUser).WithMany(p => p.Posts)
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("posts_created_by_user_id_fkey");
    }
}
