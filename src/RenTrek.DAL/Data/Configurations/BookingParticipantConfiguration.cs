using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể booking participant.
/// </summary>
public sealed class BookingParticipantConfiguration : IEntityTypeConfiguration<BookingParticipant>
{
    public void Configure(EntityTypeBuilder<BookingParticipant> builder)
    {
        builder.ToTable("booking_participants");
        builder.HasKey(x => x.ParticipantId).HasName("booking_participants_pkey");
        builder.Property(x => x.ParticipantId).HasColumnName("participant_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.BookingOrderId).HasColumnName("booking_order_id");
        builder.Property(x => x.LinkedUserId).HasColumnName("linked_user_id");
        builder.Property(x => x.TripGroupId).HasColumnName("trip_group_id");
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
        builder.Property(x => x.TripId).HasColumnName("trip_id");
        builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
        builder.Property(x => x.EmergencyContactName).HasColumnName("emergency_contact_name").HasMaxLength(255);
        builder.Property(x => x.EmergencyContactPhone).HasColumnName("emergency_contact_phone").HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => x.BookingOrderId, "ix_booking_participants_booking_order_id");
        builder.HasIndex(x => x.LinkedUserId, "ix_booking_participants_linked_user_id");
        builder.HasIndex(x => x.TripGroupId, "ix_booking_participants_trip_group_id");
        builder.HasIndex(x => x.TripId, "ix_booking_participants_trip_id");
        builder.HasOne(x => x.BookingOrder)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.BookingOrderId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("booking_participants_booking_order_id_fkey");
        builder.HasOne(x => x.LinkedUser)
            .WithMany(x => x.BookingParticipants)
            .HasForeignKey(x => x.LinkedUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("booking_participants_linked_user_id_fkey");
        builder.HasOne(x => x.TripGroup)
            .WithMany(x => x.BookingParticipants)
            .HasForeignKey(x => x.TripGroupId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("booking_participants_trip_group_id_fkey");
        builder.HasOne(x => x.Trip)
            .WithMany(x => x.BookingParticipants)
            .HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("booking_participants_trip_id_fkey");
    }
}
