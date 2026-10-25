using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TripGroupConfiguration : IEntityTypeConfiguration<TripGroup>
{
    public void Configure(EntityTypeBuilder<TripGroup> builder)
    {
        builder.HasKey(e => e.TripGroupId).HasName("trip_groups_pkey");

        builder.ToTable("trip_groups");

        builder.HasIndex(e => e.GuideUserId, "ix_trip_groups_guide_user_id");
        builder.HasIndex(e => e.TripId, "ix_trip_groups_trip_id");

        builder.Property(e => e.TripGroupId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("trip_group_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.GroupName)
            .HasMaxLength(255)
            .HasColumnName("group_name");
        builder.Property(e => e.GuideUserId).HasColumnName("guide_user_id");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.MaxParticipants).HasColumnName("max_participants");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.TripId).HasColumnName("trip_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.GuideUser).WithMany(p => p.TripGroups)
            .HasForeignKey(d => d.GuideUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("trip_groups_guide_user_id_fkey");

        builder.HasOne(d => d.Trip).WithMany(p => p.TripGroups)
            .HasForeignKey(d => d.TripId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("trip_groups_trip_id_fkey");
    }
}
