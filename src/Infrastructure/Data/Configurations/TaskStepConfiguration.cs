using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TaskStepConfiguration : IEntityTypeConfiguration<TaskStep>
{
    public void Configure(EntityTypeBuilder<TaskStep> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(s => s.Instructions)
            .HasColumnType("nvarchar(max)");

        builder.HasOne(s => s.Task)
            .WithMany(t => t.Steps)
            .HasForeignKey(s => s.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.CompletedBy)
            .WithMany()
            .HasForeignKey(s => s.CompletedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.TaskId, s.SortOrder });
    }
}
