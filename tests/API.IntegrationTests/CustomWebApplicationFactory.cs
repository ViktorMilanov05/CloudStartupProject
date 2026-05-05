using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace API.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove ALL existing DbContext/EF registrations to avoid dual-provider conflict
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(DbContextOptions)
                    || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();
            foreach (var d in descriptorsToRemove)
                services.Remove(d);

            // Add EF Core InMemory database
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });
        });

        builder.UseEnvironment("Development");
    }
}

