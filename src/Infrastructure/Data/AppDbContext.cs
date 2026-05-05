using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskStep> TaskSteps => Set<TaskStep>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateStep> TemplateSteps => Set<TemplateStep>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
