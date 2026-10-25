using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.HasKey(e => e.PromotionId).HasName("promotions_pkey");

        builder.ToTable("promotions");

        builder.HasIndex(e => e.CreatedByUserId, "ix_promotions_created_by_user_id");
        builder.HasIndex(e => e.PromotionCode, "promotions_promotion_code_key").IsUnique();

        builder.Property(e => e.PromotionId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("promotion_id");
        builder.Property(e => e.ApplicationScope)
            .HasMaxLength(50)
            .HasColumnName("application_scope");
        builder.Property(e => e.AppliesTo)
            .HasMaxLength(50)
            .HasColumnName("applies_to");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.EndAt).HasColumnName("end_at");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");
        builder.Property(e => e.IsStackable)
            .HasDefaultValue(false)
            .HasColumnName("is_stackable");
        builder.Property(e => e.MaxUses).HasColumnName("max_uses");
        builder.Property(e => e.MaxUsesPerUser).HasColumnName("max_uses_per_user");
        builder.Property(e => e.Name)
            .HasMaxLength(255)
            .HasColumnName("name");
        builder.Property(e => e.Priority)
            .HasDefaultValue(0)
            .HasColumnName("priority");
        builder.Property(e => e.PromotionCode)
            .HasMaxLength(50)
            .HasColumnName("promotion_code");
        builder.Property(e => e.StartAt).HasColumnName("start_at");
        builder.Property(e => e.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasOne(d => d.CreatedByUser).WithMany(p => p.Promotions)
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("promotions_created_by_user_id_fkey");
    }
}
