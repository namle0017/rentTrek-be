using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

/// <summary>
/// Cấu hình ánh xạ EF Core cho bảng rental_settlements.
/// </summary>
public class RentalSettlementConfiguration : IEntityTypeConfiguration<RentalSettlement>
{
    public void Configure(EntityTypeBuilder<RentalSettlement> builder)
    {
        builder.HasKey(e => e.RentalOrderId).HasName("rental_settlements_pkey");
        builder.ToTable("rental_settlements");

        builder.Property(e => e.RentalOrderId).ValueGeneratedNever().HasColumnName("rental_order_id");
        builder.Property(e => e.RentalFeePaid).HasPrecision(18).HasColumnName("rental_fee_paid");
        builder.Property(e => e.DepositHeld).HasPrecision(18).HasColumnName("deposit_held");
        builder.Property(e => e.TotalCharges).HasPrecision(18).HasColumnName("total_charges");
        builder.Property(e => e.RentalFeeRefund).HasPrecision(18).HasDefaultValue(0m).HasColumnName("rental_fee_refund");
        builder.Property(e => e.DepositRefund).HasPrecision(18).HasDefaultValue(0m).HasColumnName("deposit_refund");
        builder.Property(e => e.AmountOwed).HasPrecision(18).HasDefaultValue(0m).HasColumnName("amount_owed");
        builder.Property(e => e.RefundCaseId).HasColumnName("refund_case_id");
        builder.Property(e => e.ExtraPaymentId).HasColumnName("extra_payment_id");
        builder.Property(e => e.SettledByUserId).HasColumnName("settled_by_user_id");
        builder.Property(e => e.SettledAt).HasDefaultValueSql("now()").HasColumnName("settled_at");

        builder.HasOne(d => d.RentalOrder).WithOne(p => p.Settlement)
            .HasForeignKey<RentalSettlement>(d => d.RentalOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("rental_settlements_rental_order_id_fkey");
        builder.HasOne(d => d.RefundCase).WithMany()
            .HasForeignKey(d => d.RefundCaseId)
            .HasConstraintName("rental_settlements_refund_case_id_fkey");
        builder.HasOne(d => d.ExtraPayment).WithMany()
            .HasForeignKey(d => d.ExtraPaymentId)
            .HasConstraintName("rental_settlements_extra_payment_id_fkey");
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(d => d.SettledByUserId)
            .HasConstraintName("rental_settlements_settled_by_user_id_fkey");
    }
}
