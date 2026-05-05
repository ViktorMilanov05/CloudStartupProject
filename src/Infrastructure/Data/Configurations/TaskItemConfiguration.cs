using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(t => t.Description)
            .HasColumnType("nvarchar(max)");

        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.Priority).IsRequired();

        builder.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(t => t.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(t => t.CreatedBy)
            .WithMany(u => u.CreatedTasks)
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Assignees)
            .WithMany(u => u.AssignedTasks)
            .UsingEntity("TaskAssignees",
                j => j.HasOne(typeof(User)).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne(typeof(TaskItem)).WithMany().HasForeignKey("TaskItemId").OnDelete(DeleteBehavior.Cascade));

        builder.HasOne(t => t.SourceTemplate)
            .WithMany()
            .HasForeignKey(t => t.SourceTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.DueDate);
    }
}
