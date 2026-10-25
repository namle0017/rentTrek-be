using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TourActivityConfiguration : IEntityTypeConfiguration<TourActivity>
{
    public void Configure(EntityTypeBuilder<TourActivity> builder)
    {
        builder.ToTable("TourActivities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Description)
            .HasMaxLength(2000);
    }
}
