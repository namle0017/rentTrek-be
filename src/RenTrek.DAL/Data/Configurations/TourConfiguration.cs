using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TourConfiguration : IEntityTypeConfiguration<Tour>
{
    public void Configure(EntityTypeBuilder<Tour> builder)
    {
        builder.ToTable("Tours");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(4000);

        builder.Property(t => t.DifficultyLevel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(t => t.BasePrice)
            .HasPrecision(18, 0);

        builder.Property(t => t.EquipmentDeposit)
            .HasPrecision(18, 0);

        // Relationships
        builder.HasOne(t => t.Category)
            .WithMany(c => c.Tours)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.MainLocation)
            .WithMany(l => l.Tours)
            .HasForeignKey(t => t.MainLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Images)
            .WithOne(i => i.Tour)
            .HasForeignKey(i => i.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Activities)
            .WithOne(a => a.Tour)
            .HasForeignKey(a => a.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.PreparationItems)
            .WithOne(p => p.Tour)
            .HasForeignKey(p => p.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.ProvidedItems)
            .WithOne(p => p.Tour)
            .HasForeignKey(p => p.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Trips)
            .WithOne(tr => tr.Tour)
            .HasForeignKey(tr => tr.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.TourFamilyId, t.Version })
            .IsUnique();
    }
}
