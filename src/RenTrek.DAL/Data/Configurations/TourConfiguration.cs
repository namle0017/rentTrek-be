using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho thực thể tour.
/// </summary>
public class TourConfiguration : IEntityTypeConfiguration<Tour>
{
    public void Configure(EntityTypeBuilder<Tour> builder)
    {
        builder.ToTable("tours", table =>
        {
            table.HasCheckConstraint("tours_group_max_participants_check", "((group_max_participants IS NULL) OR (group_max_participants > 0))");
        });

        builder.HasKey(t => t.Id).HasName("tours_pkey");
        builder.Property(t => t.Id).HasColumnName("tour_id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.Name)
            .IsRequired().HasMaxLength(255);

        builder.Property(t => t.Description).HasColumnType("text");

        builder.Property(t => t.DifficultyLevel)
            .IsRequired()
            .HasMaxLength(50).HasDefaultValueSql("'Trung b\u00ecnh'::character varying");

        builder.Property(t => t.Status)
            .IsRequired().HasMaxLength(50);

        builder.Property(t => t.BasePrice)
            .HasPrecision(18, 0);

        builder.Property(t => t.EquipmentDeposit)
            .HasPrecision(18, 0);

        builder.Property(t => t.GroupMaxParticipants)
            .HasColumnName("group_max_participants");

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedByUserId).IsRequired();
        builder.Property(x => x.InspectionRate).HasDefaultValue(0);
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.Version).HasDefaultValue(1);
        builder.HasIndex(x => x.CategoryId, "ix_tours_category_id");
        builder.HasIndex(x => x.CreatedByUserId, "ix_tours_created_by_user_id");
        builder.HasIndex(x => x.MainLocationId, "ix_tours_main_location_id");
        builder.HasIndex(x => new { x.TourFamilyId, x.Version }, "tours_tour_family_id_version_key").IsUnique();
        builder.HasOne(x => x.Category)
            .WithMany(x => x.Tours)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("tours_category_id_fkey");
        builder.HasOne(x => x.CreatedByUser)
            .WithMany(x => x.Tours)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("tours_created_by_user_id_fkey");
        builder.HasOne(x => x.MainLocation)
            .WithMany(x => x.Tours)
            .HasForeignKey(x => x.MainLocationId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("tours_main_location_id_fkey");
    }
}
