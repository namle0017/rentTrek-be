using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class TourPreparationItemConfiguration : IEntityTypeConfiguration<TourPreparationItem>
{
    public void Configure(EntityTypeBuilder<TourPreparationItem> builder)
    {
        builder.HasKey(e => e.PreparationItemId).HasName("tour_preparation_items_pkey");

        builder.ToTable("tour_preparation_items");

        builder.HasIndex(e => e.TourId, "ix_tour_preparation_items_tour_id");

        builder.Property(e => e.PreparationItemId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("preparation_item_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.ItemName)
            .HasMaxLength(255)
            .HasColumnName("item_name");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.TourId).HasColumnName("tour_id");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Tour).WithMany(p => p.TourPreparationItems)
            .HasForeignKey(d => d.TourId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tour_preparation_items_tour_id_fkey");
    }
}
