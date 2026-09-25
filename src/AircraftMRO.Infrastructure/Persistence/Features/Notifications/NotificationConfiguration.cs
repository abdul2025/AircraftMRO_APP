using AircraftMRO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AircraftMRO.Infrastructure.Persistence.Features.Notifications;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        // bigint IDENTITY: increasing ids let the dispatcher read "everything after id N".
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedOnAdd();

        builder.Property(notification => notification.Action).HasConversion<int>();
        builder.Property(notification => notification.EntityType)
            .HasMaxLength(Notification.EntityTypeMaxLength)
            .IsRequired();
        builder.Property(notification => notification.EntityDisplayName)
            .HasMaxLength(Notification.EntityDisplayNameMaxLength)
            .IsRequired();
        builder.Property(notification => notification.Message)
            .HasMaxLength(Notification.MessageMaxLength)
            .IsRequired();
        builder.Property(notification => notification.ChangesJson)
            .HasMaxLength(Notification.ChangesMaxLength);
        builder.Property(notification => notification.ActorUserId)
            .HasMaxLength(Notification.ActorUserIdMaxLength);

        builder.HasIndex(notification => notification.OccurredAtUtc);
        builder.HasIndex(notification => new { notification.EntityType, notification.EntityId });
    }
}
