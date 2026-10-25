using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.HasKey(e => e.LocationId).HasName("locations_pkey");

        builder.ToTable("locations");

        builder.Property(e => e.LocationId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("location_id");
        builder.Property(e => e.Address)
            .HasMaxLength(500)
            .HasColumnName("address");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.Latitude)
            .HasPrecision(9, 6)
            .HasColumnName("latitude");
        builder.Property(e => e.Longitude)
            .HasPrecision(9, 6)
            .HasColumnName("longitude");
        builder.Property(e => e.Name)
            .HasMaxLength(255)
            .HasColumnName("name");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
    }
}
