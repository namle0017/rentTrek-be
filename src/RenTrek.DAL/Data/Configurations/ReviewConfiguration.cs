using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(e => e.ReviewId).HasName("reviews_pkey");

        builder.ToTable("reviews");

        builder.HasIndex(e => e.ParticipantId, "reviews_participant_id_key").IsUnique();

        builder.Property(e => e.ReviewId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("review_id");
        builder.Property(e => e.Comment).HasColumnName("comment");
        builder.Property(e => e.IsHidden).HasDefaultValue(false).HasColumnName("is_hidden");
        builder.Property(e => e.HiddenReason).HasColumnName("hidden_reason");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.ParticipantId).HasColumnName("participant_id");
        builder.Property(e => e.Rating).HasColumnName("rating");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Participant).WithOne(p => p.Review)
            .HasForeignKey<Review>(d => d.ParticipantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("reviews_participant_id_fkey");
    }
}
