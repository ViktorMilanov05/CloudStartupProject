using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(t => t.Description)
            .HasColumnType("nvarchar(max)");

        builder.Property(t => t.IsActive).HasDefaultValue(true);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(t => t.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
