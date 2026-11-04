using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(e => e.TagId).HasName("tags_pkey");

        builder.ToTable("tags");

        builder.HasIndex(e => e.Name, "tags_name_key").IsUnique();
        builder.HasIndex(e => e.Slug, "tags_slug_key").IsUnique();

        builder.Property(e => e.TagId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("tag_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.Name)
            .HasMaxLength(100)
            .HasColumnName("name");
        builder.Property(e => e.Slug)
            .HasMaxLength(100)
            .HasColumnName("slug");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
    }
}
