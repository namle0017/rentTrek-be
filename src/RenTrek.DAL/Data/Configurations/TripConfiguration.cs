using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể trip.
/// </summary>
public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("trips", table =>
        {
            table.HasCheckConstraint("trips_check", "(end_at > start_at)");
            table.HasCheckConstraint("trips_total_slots_check", "(total_slots >= 0)");
        });
        builder.HasKey(x => x.TripId).HasName("trips_pkey");
        builder.Property(x => x.TripId).HasColumnName("trip_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.TourId).HasColumnName("tour_id");
        builder.Property(x => x.RecurrenceRuleId).HasColumnName("recurrence_rule_id");
        builder.Property(x => x.RefundPolicyId).HasColumnName("refund_policy_id");
        builder.Property(x => x.ManagedByUserId).HasColumnName("managed_by_user_id");
        builder.Property(x => x.StartAt).HasColumnName("start_at");
        builder.Property(x => x.EndAt).HasColumnName("end_at");
        builder.Property(x => x.TotalSlots).HasColumnName("total_slots");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
        builder.Property(x => x.PricePerPerson).HasColumnName("price_per_person").HasPrecision(18, 0);
        builder.Property(x => x.MinParticipants).HasColumnName("min_participants");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken().HasDefaultValue(1);
        builder.Property(x => x.ProvidedItemsNeedsRecalc).HasColumnName("provided_items_needs_recalc").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Ignore(x => x.BookedSlots);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => x.ManagedByUserId, "ix_trips_managed_by_user_id");
        builder.HasIndex(x => x.RecurrenceRuleId, "ix_trips_recurrence_rule_id");
        builder.HasIndex(x => x.RefundPolicyId, "ix_trips_refund_policy_id");
        builder.HasIndex(x => x.TourId, "ix_trips_tour_id");
        builder.HasIndex(x => new { x.RecurrenceRuleId, x.StartAt }, "ux_trips_recurrence_start_at")
            .IsUnique()
            .HasFilter("((recurrence_rule_id IS NOT NULL) AND (NOT is_deleted))");
        builder.HasOne(x => x.ManagedByUser)
            .WithMany(x => x.Trips)
            .HasForeignKey(x => x.ManagedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("trips_managed_by_user_id_fkey");
        builder.HasOne(x => x.RecurrenceRule)
            .WithMany(x => x.Trips)
            .HasForeignKey(x => x.RecurrenceRuleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("trips_recurrence_rule_id_fkey");
        builder.HasOne(x => x.RefundPolicy)
            .WithMany(x => x.Trips)
            .HasForeignKey(x => x.RefundPolicyId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("trips_refund_policy_id_fkey");
        builder.HasOne(x => x.Tour)
            .WithMany(x => x.Trips)
            .HasForeignKey(x => x.TourId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("trips_tour_id_fkey");
    }
}
