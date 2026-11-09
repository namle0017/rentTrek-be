using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class RentalOrderConfiguration : IEntityTypeConfiguration<RentalOrder>
{
    public void Configure(EntityTypeBuilder<RentalOrder> builder)
    {
        builder.HasKey(e => e.OrderId).HasName("rental_orders_pkey");

        builder.ToTable("rental_orders", table =>
        {
            table.HasCheckConstraint("rental_orders_check", "(rental_end_at > rental_start_at)");
        });

        builder.HasIndex(e => e.HandledByUserId, "ix_rental_orders_handled_by_user_id");

        builder.Property(e => e.OrderId)
            .ValueGeneratedNever()
            .HasColumnName("order_id");
        builder.Property(e => e.DepositAmount)
            .HasPrecision(18)
            .HasColumnName("deposit_amount");
        builder.Property(e => e.HandledByUserId).HasColumnName("handled_by_user_id");
        builder.Property(e => e.RentalEndAt).HasColumnName("rental_end_at");
        builder.Property(e => e.RentalStartAt).HasColumnName("rental_start_at");

        builder.HasOne(d => d.HandledByUser).WithMany(p => p.RentalOrders)
            .HasForeignKey(d => d.HandledByUserId)
            .HasConstraintName("rental_orders_handled_by_user_id_fkey");

        builder.HasOne(d => d.Order).WithOne(p => p.RentalOrder)
            .HasForeignKey<RentalOrder>(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_orders_order_id_fkey");
    }
}
