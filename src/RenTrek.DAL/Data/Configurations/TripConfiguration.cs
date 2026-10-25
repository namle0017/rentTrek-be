using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("trips");
        builder.HasKey(x => x.TripId);
        builder.Property(x => x.TripId).HasColumnName("trip_id");
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
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        builder.Ignore(x => x.BookedSlots);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
