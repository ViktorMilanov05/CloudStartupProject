using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
    }
}
