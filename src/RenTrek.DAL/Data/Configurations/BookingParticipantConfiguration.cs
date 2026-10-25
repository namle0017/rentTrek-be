using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class BookingParticipantConfiguration : IEntityTypeConfiguration<BookingParticipant>
{
    public void Configure(EntityTypeBuilder<BookingParticipant> builder)
    {
        builder.HasKey(e => e.ParticipantId).HasName("booking_participants_pkey");

        builder.ToTable("booking_participants");

        builder.HasIndex(e => e.BookingOrderId, "ix_booking_participants_booking_order_id");
        builder.HasIndex(e => e.LinkedUserId, "ix_booking_participants_linked_user_id");
        builder.HasIndex(e => e.TripGroupId, "ix_booking_participants_trip_group_id");
        builder.HasIndex(e => e.TripId, "ix_booking_participants_trip_id");

        builder.Property(e => e.ParticipantId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("participant_id");
        builder.Property(e => e.BookingOrderId).HasColumnName("booking_order_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
        builder.Property(e => e.Email)
            .HasMaxLength(255)
            .HasColumnName("email");
        builder.Property(e => e.EmergencyContactName)
            .HasMaxLength(255)
            .HasColumnName("emergency_contact_name");
        builder.Property(e => e.EmergencyContactPhone)
            .HasMaxLength(20)
            .HasColumnName("emergency_contact_phone");
        builder.Property(e => e.FullName)
            .HasMaxLength(255)
            .HasColumnName("full_name");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.LinkedUserId).HasColumnName("linked_user_id");
        builder.Property(e => e.Phone)
            .HasMaxLength(20)
            .HasColumnName("phone");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.TripGroupId).HasColumnName("trip_group_id");
        builder.Property(e => e.TripId).HasColumnName("trip_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.BookingOrder).WithMany(p => p.BookingParticipants)
            .HasForeignKey(d => d.BookingOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("booking_participants_booking_order_id_fkey");

        builder.HasOne(d => d.LinkedUser).WithMany(p => p.BookingParticipants)
            .HasForeignKey(d => d.LinkedUserId)
            .HasConstraintName("booking_participants_linked_user_id_fkey");

        builder.HasOne(d => d.TripGroup).WithMany(p => p.BookingParticipants)
            .HasForeignKey(d => d.TripGroupId)
            .HasConstraintName("booking_participants_trip_group_id_fkey");

        builder.HasOne(d => d.Trip).WithMany(p => p.BookingParticipants)
            .HasForeignKey(d => d.TripId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("booking_participants_trip_id_fkey");
    }
}
