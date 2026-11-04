using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể tour activity.
/// </summary>
public class TourActivityConfiguration : IEntityTypeConfiguration<TourActivity>
{
    public void Configure(EntityTypeBuilder<TourActivity> builder)
    {
        builder.ToTable("tour_activities");

        builder.HasKey(a => a.Id).HasName("tour_activities_pkey");
        builder.Property(a => a.Id).HasColumnName("tour_activity_id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.Name)
            .IsRequired().HasMaxLength(255);

        builder.Property(a => a.Description).HasColumnType("text");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0);
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.DayTitle).HasMaxLength(255);
        builder.Property(x => x.StartTime).HasColumnType("time(0) without time zone");
        builder.HasIndex(x => x.TourId, "ix_tour_activities_tour_id");
        builder.HasOne(x => x.Tour)
            .WithMany(x => x.Activities)
            .HasForeignKey(x => x.TourId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("tour_activities_tour_id_fkey");
    }
}
