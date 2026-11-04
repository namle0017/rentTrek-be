using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(e => e.NotificationId).HasName("notifications_pkey");

        builder.ToTable("notifications");

        builder.HasIndex(e => e.RecipientUserId, "ix_notifications_recipient_user_id");

        builder.Property(e => e.NotificationId)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("notification_id");
        builder.Property(e => e.Content)
            .HasMaxLength(2000)
            .HasColumnName("content");
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(e => e.NotificationType)
            .HasMaxLength(50)
            .HasColumnName("notification_type");
        builder.Property(e => e.ReadAt).HasColumnName("read_at");
        builder.Property(e => e.RecipientUserId).HasColumnName("recipient_user_id");
        builder.Property(e => e.RelatedEntityId).HasColumnName("related_entity_id");
        builder.Property(e => e.RelatedEntityType)
            .HasMaxLength(50)
            .HasColumnName("related_entity_type");
        builder.Property(e => e.Title)
            .HasMaxLength(255)
            .HasColumnName("title");

        builder.HasOne(d => d.RecipientUser).WithMany(p => p.Notifications)
            .HasForeignKey(d => d.RecipientUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("notifications_recipient_user_id_fkey");
    }
}
