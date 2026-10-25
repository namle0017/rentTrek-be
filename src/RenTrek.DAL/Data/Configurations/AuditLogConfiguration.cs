using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(e => e.AuditLogId).HasName("audit_logs_pkey");

        builder.ToTable("audit_logs");

        builder.HasIndex(e => e.ActorUserId, "ix_audit_logs_actor_user_id");

        builder.Property(e => e.AuditLogId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("audit_log_id");
        builder.Property(e => e.Action)
            .HasMaxLength(100)
            .HasColumnName("action");
        builder.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.Details).HasColumnName("details");
        builder.Property(e => e.EntityId).HasColumnName("entity_id");
        builder.Property(e => e.EntityType)
            .HasMaxLength(100)
            .HasColumnName("entity_type");

        builder.HasOne(d => d.ActorUser).WithMany(p => p.AuditLogs)
            .HasForeignKey(d => d.ActorUserId)
            .HasConstraintName("audit_logs_actor_user_id_fkey");
    }
}
