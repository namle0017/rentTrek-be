using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TourRecurrenceRuleConfiguration : IEntityTypeConfiguration<TourRecurrenceRule>
{
    public void Configure(EntityTypeBuilder<TourRecurrenceRule> builder)
    {
        builder.HasKey(e => e.RecurrenceRuleId).HasName("tour_recurrence_rules_pkey");

        builder.ToTable("tour_recurrence_rules");

        builder.HasIndex(e => e.TourId, "ix_tour_recurrence_rules_tour_id");

        builder.Property(e => e.RecurrenceRuleId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("recurrence_rule_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.DaysOfWeek)
            .HasMaxLength(50)
            .HasColumnName("days_of_week");
        builder.Property(e => e.DefaultPrice)
            .HasPrecision(18)
            .HasColumnName("default_price");
        builder.Property(e => e.DefaultTotalSlots).HasColumnName("default_total_slots");
        builder.Property(e => e.EndDate).HasColumnName("end_date");
        builder.Property(e => e.Frequency)
            .HasMaxLength(50)
            .HasColumnName("frequency");
        builder.Property(e => e.IntervalValue)
            .HasDefaultValue(1)
            .HasColumnName("interval_value");
        builder.Property(e => e.IsActive)
            .HasDefaultValue(true)
            .HasColumnName("is_active");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.StartDate).HasColumnName("start_date");
        builder.Property(e => e.StartTime)
            .HasPrecision(0)
            .HasColumnName("start_time");
        builder.Property(e => e.TourId).HasColumnName("tour_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Tour).WithMany(p => p.TourRecurrenceRules)
            .HasForeignKey(d => d.TourId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tour_recurrence_rules_tour_id_fkey");
    }
}
