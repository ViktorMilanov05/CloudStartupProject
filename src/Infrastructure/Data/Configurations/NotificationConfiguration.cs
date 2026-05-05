using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(n => n.Message).IsRequired().HasMaxLength(500);
        builder.Property(n => n.TaskTitle).HasMaxLength(300);
        builder.Property(n => n.ActorName).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Type).IsRequired();
        builder.Property(n => n.IsRead).HasDefaultValue(false);
        builder.Property(n => n.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
        builder.HasIndex(n => n.CreatedAt); // for cleanup
    }
}
