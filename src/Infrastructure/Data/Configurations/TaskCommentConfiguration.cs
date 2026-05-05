using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(c => c.Task)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
