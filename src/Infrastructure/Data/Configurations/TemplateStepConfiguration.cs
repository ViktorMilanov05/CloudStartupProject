using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TemplateStepConfiguration : IEntityTypeConfiguration<TemplateStep>
{
    public void Configure(EntityTypeBuilder<TemplateStep> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(s => s.Instructions)
            .HasColumnType("nvarchar(max)");

        builder.HasOne(s => s.Template)
            .WithMany(t => t.Steps)
            .HasForeignKey(s => s.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.TemplateId, s.SortOrder });
    }
}
