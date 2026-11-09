using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TourPostConfiguration : IEntityTypeConfiguration<TourPost>
{
    public void Configure(EntityTypeBuilder<TourPost> builder)
    {
        builder.HasKey(e => e.TourPostId).HasName("tour_posts_pkey");

        builder.ToTable("tour_posts");

        builder.HasIndex(e => e.PostId, "ix_tour_posts_post_id");
        builder.HasIndex(e => new { e.TourId, e.PostId }, "tour_posts_tour_id_post_id_key").IsUnique();

        builder.Property(e => e.TourPostId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("tour_post_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.DisplayOrder)
            .HasDefaultValue(0)
            .HasColumnName("display_order");
        builder.Property(e => e.PostId).HasColumnName("post_id");
        builder.Property(e => e.TourId).HasColumnName("tour_id");

        builder.HasOne(d => d.Post).WithMany(p => p.TourPosts)
            .HasForeignKey(d => d.PostId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tour_posts_post_id_fkey");

        builder.HasOne(d => d.Tour).WithMany(p => p.TourPosts)
            .HasForeignKey(d => d.TourId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tour_posts_tour_id_fkey");
    }
}
