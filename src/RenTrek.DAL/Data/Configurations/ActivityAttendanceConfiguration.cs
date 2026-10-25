using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class ActivityAttendanceConfiguration : IEntityTypeConfiguration<ActivityAttendance>
{
    public void Configure(EntityTypeBuilder<ActivityAttendance> builder)
    {
        builder.HasKey(e => e.AttendanceId).HasName("activity_attendances_pkey");

        builder.ToTable("activity_attendances");

        builder.HasIndex(e => new { e.TourActivityId, e.ParticipantId }, "activity_attendances_tour_activity_id_participant_id_key").IsUnique();
        builder.HasIndex(e => e.CheckedByUserId, "ix_activity_attendances_checked_by_user_id");
        builder.HasIndex(e => e.ParticipantId, "ix_activity_attendances_participant_id");

        builder.Property(e => e.AttendanceId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("attendance_id");
        builder.Property(e => e.AttendanceStatus)
            .HasMaxLength(50)
            .HasColumnName("attendance_status");
        builder.Property(e => e.CheckedAt).HasColumnName("checked_at");
        builder.Property(e => e.CheckedByUserId).HasColumnName("checked_by_user_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.ParticipantId).HasColumnName("participant_id");
        builder.Property(e => e.TourActivityId).HasColumnName("tour_activity_id");

        builder.HasOne(d => d.CheckedByUser).WithMany(p => p.ActivityAttendances)
            .HasForeignKey(d => d.CheckedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("activity_attendances_checked_by_user_id_fkey");

        builder.HasOne(d => d.Participant).WithMany(p => p.ActivityAttendances)
            .HasForeignKey(d => d.ParticipantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("activity_attendances_participant_id_fkey");

        builder.HasOne(d => d.TourActivity).WithMany(p => p.ActivityAttendances)
            .HasForeignKey(d => d.TourActivityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("activity_attendances_tour_activity_id_fkey");
    }
}
