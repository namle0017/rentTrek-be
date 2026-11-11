using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class ParticipantPostReadConfiguration : IEntityTypeConfiguration<ParticipantPostRead>
{
    public void Configure(EntityTypeBuilder<ParticipantPostRead> builder)
    {
        builder.HasKey(e => e.ParticipantPostReadId).HasName("participant_post_reads_pkey");

        builder.ToTable("participant_post_reads");

        builder.HasIndex(e => e.PostId, "ix_participant_post_reads_post_id");
        builder.HasIndex(e => new { e.ParticipantId, e.PostId }, "participant_post_reads_participant_id_post_id_key").IsUnique();

        builder.Property(e => e.ParticipantPostReadId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("participant_post_read_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.ParticipantId).HasColumnName("participant_id");
        builder.Property(e => e.PostId).HasColumnName("post_id");
        builder.Property(e => e.ReadAt).HasColumnName("read_at");

        builder.HasOne(d => d.Participant).WithMany(p => p.ParticipantPostReads)
            .HasForeignKey(d => d.ParticipantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("participant_post_reads_participant_id_fkey");

        builder.HasOne(d => d.Post).WithMany(p => p.ParticipantPostReads)
            .HasForeignKey(d => d.PostId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("participant_post_reads_post_id_fkey");
    }
}
