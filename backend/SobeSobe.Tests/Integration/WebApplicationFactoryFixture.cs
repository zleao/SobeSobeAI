using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SobeSobe.Infrastructure.Data;

namespace SobeSobe.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration tests with shared in-memory SQLite database
/// </summary>
public class WebApplicationFactoryFixture : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Remove the UseInMemoryDatabase flag since we're overriding DbContext configuration
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing ApplicationDbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Create and open a shared in-memory SQLite connection
            // Using "Mode=Memory;Cache=Shared" ensures the database persists across requests
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add ApplicationDbContext with shared in-memory SQLite connection
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Ensure database schema is created
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }
}

