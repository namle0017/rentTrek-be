using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TourActivityConfiguration : IEntityTypeConfiguration<TourActivity>
{
    public void Configure(EntityTypeBuilder<TourActivity> builder)
    {
        builder.HasKey(e => e.TourActivityId).HasName("tour_activities_pkey");

        builder.ToTable("tour_activities");

        builder.HasIndex(e => e.TourId, "ix_tour_activities_tour_id");

        builder.Property(e => e.TourActivityId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("tour_activity_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.DayNumber).HasColumnName("day_number");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.DisplayOrder)
            .HasDefaultValue(0)
            .HasColumnName("display_order");
        builder.Property(e => e.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.Name)
            .HasMaxLength(255)
            .HasColumnName("name");
        builder.Property(e => e.StartTime)
            .HasPrecision(0)
            .HasColumnName("start_time");
        builder.Property(e => e.TourId).HasColumnName("tour_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Tour).WithMany(p => p.TourActivities)
            .HasForeignKey(d => d.TourId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tour_activities_tour_id_fkey");
    }
}
