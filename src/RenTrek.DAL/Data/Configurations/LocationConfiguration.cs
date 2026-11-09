using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể location.
/// </summary>
public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");

        builder.HasKey(l => l.Id).HasName("locations_pkey");
        builder.Property(l => l.Id).HasColumnName("location_id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.Name)
            .IsRequired().HasMaxLength(255);

        builder.Property(l => l.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(l => l.Latitude)
            .HasPrecision(9, 6);

        builder.Property(l => l.Longitude)
            .HasPrecision(9, 6);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
    }
}
