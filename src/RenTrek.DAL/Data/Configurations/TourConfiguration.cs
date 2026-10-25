using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TourConfiguration : IEntityTypeConfiguration<Tour>
{
    public void Configure(EntityTypeBuilder<Tour> builder)
    {
        builder.HasKey(e => e.TourId).HasName("tours_pkey");

        builder.ToTable("tours");

        builder.HasIndex(e => e.CategoryId, "ix_tours_category_id");
        builder.HasIndex(e => e.CreatedByUserId, "ix_tours_created_by_user_id");
        builder.HasIndex(e => e.MainLocationId, "ix_tours_main_location_id");
        builder.HasIndex(e => new { e.TourFamilyId, e.Version }, "tours_tour_family_id_version_key").IsUnique();

        builder.Property(e => e.TourId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("tour_id");
        builder.Property(e => e.BasePrice)
            .HasPrecision(18)
            .HasColumnName("base_price");
        builder.Property(e => e.CategoryId).HasColumnName("category_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.DurationDays).HasColumnName("duration_days");
        builder.Property(e => e.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(e => e.DurationNights).HasColumnName("duration_nights");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.ItineraryLockedAt).HasColumnName("itinerary_locked_at");
        builder.Property(e => e.MainLocationId).HasColumnName("main_location_id");
        builder.Property(e => e.Name)
            .HasMaxLength(255)
            .HasColumnName("name");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.TourFamilyId).HasColumnName("tour_family_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
        builder.Property(e => e.Version)
            .HasDefaultValue(1)
            .HasColumnName("version");

        builder.HasOne(d => d.Category).WithMany(p => p.Tours)
            .HasForeignKey(d => d.CategoryId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tours_category_id_fkey");

        builder.HasOne(d => d.CreatedByUser).WithMany(p => p.Tours)
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tours_created_by_user_id_fkey");

        builder.HasOne(d => d.MainLocation).WithMany(p => p.Tours)
            .HasForeignKey(d => d.MainLocationId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tours_main_location_id_fkey");
    }
}
