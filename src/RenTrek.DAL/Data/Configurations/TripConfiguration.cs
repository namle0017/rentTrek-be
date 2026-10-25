using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.HasKey(e => e.TripId).HasName("trips_pkey");

        builder.ToTable("trips");

        builder.HasIndex(e => e.ManagedByUserId, "ix_trips_managed_by_user_id");
        builder.HasIndex(e => e.RecurrenceRuleId, "ix_trips_recurrence_rule_id");
        builder.HasIndex(e => e.RefundPolicyId, "ix_trips_refund_policy_id");
        builder.HasIndex(e => e.TourId, "ix_trips_tour_id");

        builder.Property(e => e.TripId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("trip_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.EndAt).HasColumnName("end_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.ManagedByUserId).HasColumnName("managed_by_user_id");
        builder.Property(e => e.MinParticipants).HasColumnName("min_participants");
        builder.Property(e => e.PricePerPerson)
            .HasPrecision(18)
            .HasColumnName("price_per_person");
        builder.Property(e => e.RecurrenceRuleId).HasColumnName("recurrence_rule_id");
        builder.Property(e => e.RefundPolicyId).HasColumnName("refund_policy_id");
        builder.Property(e => e.RowVersion)
            .HasDefaultValue(1)
            .HasColumnName("row_version");
        builder.Property(e => e.StartAt).HasColumnName("start_at");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.TotalSlots).HasColumnName("total_slots");
        builder.Property(e => e.TourId).HasColumnName("tour_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.ManagedByUser).WithMany(p => p.Trips)
            .HasForeignKey(d => d.ManagedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("trips_managed_by_user_id_fkey");

        builder.HasOne(d => d.RecurrenceRule).WithMany(p => p.Trips)
            .HasForeignKey(d => d.RecurrenceRuleId)
            .HasConstraintName("trips_recurrence_rule_id_fkey");

        builder.HasOne(d => d.RefundPolicy).WithMany(p => p.Trips)
            .HasForeignKey(d => d.RefundPolicyId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("trips_refund_policy_id_fkey");

        builder.HasOne(d => d.Tour).WithMany(p => p.Trips)
            .HasForeignKey(d => d.TourId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("trips_tour_id_fkey");
    }
}
